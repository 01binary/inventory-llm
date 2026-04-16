using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using InventoryDemo.Server.DTOs;
using InventoryDemo.Server.Options;

namespace InventoryDemo.Server.Services;

public sealed class LlmService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ModelServiceOptions _options;
    private readonly PromptService _PromptService;
    private readonly McpClientService _mcpClientService;
    private readonly ILogger<LlmService> _logger;

    public LlmService(
        IHttpClientFactory httpClientFactory,
        IOptions<ModelServiceOptions> options,
        PromptService PromptService,
        McpClientService mcpClientService,
        ILogger<LlmService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _PromptService = PromptService;
        _mcpClientService = mcpClientService;
        _logger = logger;
    }

    public async Task<object> CompleteAsync(ChatCompletionRequest request)
    {
        var client = _httpClientFactory.CreateClient("llm");
        IReadOnlyList<McpToolDescriptor> tools;
        try
        {
            tools = await _mcpClientService.ListToolsAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load MCP tools from {McpServerUrl}. Continuing without tool support.", _options.McpServerUrl);
            tools = Array.Empty<McpToolDescriptor>();
        }

        var model = await ResolveModelAsync();
        var conversation = BuildMessages(request);

        const int maxToolRounds = 8;
        string lastBody = string.Empty;

        for (var round = 0; round < maxToolRounds; round++)
        {
            var payload = BuildPayload(model, request.MaxTokens, conversation, tools);
            var json = JsonSerializer.Serialize(payload);
            _logger.LogInformation("Sending completion request to LLM server at {Path}", _options.LlmCompletionPath);

            using var response = await client.PostAsync(
                _options.LlmCompletionPath,
                new StringContent(json, Encoding.UTF8, "application/json"));

            lastBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("LLM server returned {StatusCode}: {Body}", response.StatusCode, lastBody);
                throw new InvalidOperationException($"LLM request failed with HTTP {(int)response.StatusCode}");
            }

            var completion = JsonSerializer.Deserialize<LlmCompletionResponseDto>(lastBody)
                ?? throw new InvalidOperationException("LLM response body could not be deserialized.");

            if (!TryGetToolCalls(completion, out var toolCalls))
            {
                var content = ExtractContent(completion, lastBody);
                return new
                {
                    text = content?.Trim() ?? string.Empty,
                    raw = JsonSerializer.Deserialize<object>(lastBody)
                };
            }

            AppendAssistantToolCallMessage(conversation, completion, toolCalls);

            await ExecuteToolCallsAsync(conversation, toolCalls, CancellationToken.None);
        }

        throw new InvalidOperationException(
            $"LLM requested more than {maxToolRounds} tool-call rounds without producing a final response.");
    }

    private object BuildPayload(
        string model,
        int maxTokens,
        IReadOnlyList<LlmConversationMessageDto> conversation,
        IReadOnlyList<McpToolDescriptor> tools)
    {
        var toolDefinitions = tools.Select(tool => new
        {
            type = "function",
            function = new
            {
                name = tool.Name,
                description = tool.Description,
                parameters = tool.JsonSchema
            }
        }).ToList();

        return new
        {
            model,
            messages = conversation,
            tools = toolDefinitions.Count > 0 ? toolDefinitions : null,
            tool_choice = toolDefinitions.Count > 0 ? "auto" : null,
            temperature = 0.2,
            max_tokens = maxTokens,
            stream = false
        };
    }

    private List<LlmConversationMessageDto> BuildMessages(ChatCompletionRequest request)
    {
        var messages = new List<LlmConversationMessageDto>();
        var systemPrompt = _PromptService.GetSystemPrompt();

        var hasSystemMessage = request.Messages?.Any(message =>
            string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase)) ?? false;

        if (!hasSystemMessage && !string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new LlmConversationMessageDto
            {
                Role = "system",
                Content = systemPrompt
            });
        }

        foreach (var message in _PromptService.GetFewShotPrompts())
        {
            messages.Add(new LlmConversationMessageDto
            {
                Role = message.Role,
                Content = message.Content
            });
        }

        if (request.Messages is { Count: > 0 })
        {
            foreach (var message in request.Messages)
            {
                if (string.IsNullOrWhiteSpace(message.Role) || string.IsNullOrWhiteSpace(message.Content))
                {
                    continue;
                }

                messages.Add(new LlmConversationMessageDto
                {
                    Role = message.Role.Trim().ToLowerInvariant(),
                    Content = message.Content.Trim()
                });
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            messages.Add(new LlmConversationMessageDto
            {
                Role = "user",
                Content = request.Prompt.Trim()
            });
        }

        if (messages.Count == 0)
        {
            throw new InvalidOperationException("No valid messages found for completion request.");
        }

        return messages;
    }

    private static bool TryGetToolCalls(
        LlmCompletionResponseDto completion,
        out IReadOnlyList<LlmToolCallDto> toolCalls)
    {
        toolCalls = completion.Choices?.FirstOrDefault()?.Message?.ToolCalls ?? Array.Empty<LlmToolCallDto>();
        return toolCalls.Count > 0;
    }

    private static void AppendAssistantToolCallMessage(
        ICollection<LlmConversationMessageDto> conversation,
        LlmCompletionResponseDto response,
        IReadOnlyList<LlmToolCallDto> toolCalls)
    {
        var firstMessage = response.Choices?.FirstOrDefault()?.Message;
        conversation.Add(new LlmConversationMessageDto
        {
            Role = "assistant",
            Content = firstMessage is null
                ? string.Empty
                : ExtractMessageContent(firstMessage.Content),
            ToolCalls = toolCalls
        });
    }

    private async Task ExecuteToolCallsAsync(
        ICollection<LlmConversationMessageDto> conversation,
        IReadOnlyList<LlmToolCallDto> toolCalls,
        CancellationToken cancellationToken)
    {
        foreach (var toolCall in toolCalls)
        {
            var toolCallId = string.IsNullOrWhiteSpace(toolCall.Id)
                ? Guid.NewGuid().ToString("N")
                : toolCall.Id;

            if (toolCall.Function is null)
            {
                continue;
            }

            var toolName = toolCall.Function.Name;
            if (string.IsNullOrWhiteSpace(toolName))
            {
                continue;
            }

            var argumentsJson = toolCall.Function.Arguments ?? "{}";

            _logger.LogInformation("Executing MCP tool call from model: {ToolName}", toolName);
            var toolResult = await _mcpClientService.CallToolAsync(toolName, argumentsJson, cancellationToken);

            conversation.Add(new LlmConversationMessageDto
            {
                Role = "tool",
                ToolCallId = toolCallId,
                Content = toolResult
            });
        }
    }

    private async Task<string> ResolveModelAsync()
    {
        if (!string.IsNullOrWhiteSpace(_options.LlmModel) &&
            !string.Equals(_options.LlmModel, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return _options.LlmModel;
        }

        var client = _httpClientFactory.CreateClient("llm");
        using var response = await client.GetAsync(_options.LlmHealthPath);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var modelList = JsonSerializer.Deserialize<LlmModelListResponseDto>(body);
        var modelId = modelList?.Data?.FirstOrDefault()?.Id;
        if (!string.IsNullOrWhiteSpace(modelId))
        {
            _logger.LogInformation("Resolved LM Studio model id {ModelId} from {Path}", modelId, _options.LlmHealthPath);
            return modelId;
        }

        throw new InvalidOperationException("Could not resolve an LM Studio model id from /v1/models. Set LLM_MODEL explicitly in .env.");
    }

    private static string ExtractMessageContent(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return string.Empty;
        }

        return content.GetRawText();
    }

    private string ExtractContent(LlmCompletionResponseDto completion, string fallbackBody)
    {
        var firstChoice = completion.Choices?.FirstOrDefault();
        if (firstChoice?.Message is { } message)
        {
            return ExtractMessageContent(message.Content);
        }

        if (!string.IsNullOrWhiteSpace(firstChoice?.Text))
        {
            return firstChoice.Text;
        }

        return fallbackBody;
    }
}
