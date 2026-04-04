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
    private readonly SystemPromptService _systemPromptService;
    private readonly McpClientService _mcpClientService;
    private readonly ILogger<LlmService> _logger;

    public LlmService(
        IHttpClientFactory httpClientFactory,
        IOptions<ModelServiceOptions> options,
        SystemPromptService systemPromptService,
        McpClientService mcpClientService,
        ILogger<LlmService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _systemPromptService = systemPromptService;
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

            using var document = JsonDocument.Parse(lastBody);
            var root = document.RootElement;

            if (!TryGetToolCalls(root, out var toolCallsElement))
            {
                var content = ExtractContent(root, lastBody);
                return new
                {
                    text = content?.Trim() ?? string.Empty,
                    raw = JsonSerializer.Deserialize<object>(lastBody)
                };
            }

            AppendAssistantToolCallMessage(conversation, root, toolCallsElement);
            await ExecuteToolCallsAsync(conversation, toolCallsElement, CancellationToken.None);
        }

        throw new InvalidOperationException(
            $"LLM requested more than {maxToolRounds} tool-call rounds without producing a final response.");
    }

    private object BuildPayload(
        string model,
        int maxTokens,
        IReadOnlyList<Dictionary<string, object?>> conversation,
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

    private List<Dictionary<string, object?>> BuildMessages(ChatCompletionRequest request)
    {
        var messages = new List<Dictionary<string, object?>>();
        var systemPrompt = _systemPromptService.GetSystemPrompt();

        var hasSystemMessage = request.Messages?.Any(message =>
            string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase)) ?? false;

        if (!hasSystemMessage && !string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new Dictionary<string, object?>
            {
                ["role"] = "system",
                ["content"] = systemPrompt
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

                messages.Add(new Dictionary<string, object?>
                {
                    ["role"] = message.Role.Trim().ToLowerInvariant(),
                    ["content"] = message.Content.Trim()
                });
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            messages.Add(new Dictionary<string, object?>
            {
                ["role"] = "user",
                ["content"] = request.Prompt.Trim()
            });
        }

        if (messages.Count == 0)
        {
            throw new InvalidOperationException("No valid messages found for completion request.");
        }

        return messages;
    }

    private static bool TryGetToolCalls(JsonElement root, out JsonElement toolCalls)
    {
        toolCalls = default;
        if (!root.TryGetProperty("choices", out var choicesElement) ||
            choicesElement.ValueKind != JsonValueKind.Array ||
            choicesElement.GetArrayLength() == 0)
        {
            return false;
        }

        var firstChoice = choicesElement[0];
        if (!firstChoice.TryGetProperty("message", out var messageElement))
        {
            return false;
        }

        if (!messageElement.TryGetProperty("tool_calls", out toolCalls))
        {
            return false;
        }

        return toolCalls.ValueKind == JsonValueKind.Array && toolCalls.GetArrayLength() > 0;
    }

    private static void AppendAssistantToolCallMessage(
        ICollection<Dictionary<string, object?>> conversation,
        JsonElement responseRoot,
        JsonElement toolCalls)
    {
        var assistantMessage = new Dictionary<string, object?>
        {
            ["role"] = "assistant"
        };

        if (responseRoot.TryGetProperty("choices", out var choicesElement) &&
            choicesElement.ValueKind == JsonValueKind.Array &&
            choicesElement.GetArrayLength() > 0 &&
            choicesElement[0].TryGetProperty("message", out var messageElement) &&
            messageElement.TryGetProperty("content", out var contentElement))
        {
            assistantMessage["content"] = contentElement.ValueKind == JsonValueKind.String
                ? contentElement.GetString()
                : contentElement.GetRawText();
        }
        else
        {
            assistantMessage["content"] = string.Empty;
        }

        assistantMessage["tool_calls"] = JsonSerializer.Deserialize<object>(toolCalls.GetRawText());
        conversation.Add(assistantMessage);
    }

    private async Task ExecuteToolCallsAsync(
        ICollection<Dictionary<string, object?>> conversation,
        JsonElement toolCalls,
        CancellationToken cancellationToken)
    {
        foreach (var toolCall in toolCalls.EnumerateArray())
        {
            var toolCallId = toolCall.TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : Guid.NewGuid().ToString("N");

            if (!toolCall.TryGetProperty("function", out var functionElement))
            {
                continue;
            }

            var toolName = functionElement.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(toolName))
            {
                continue;
            }

            var argumentsJson = functionElement.TryGetProperty("arguments", out var argumentsElement)
                ? argumentsElement.GetString()
                : "{}";

            _logger.LogInformation("Executing MCP tool call from model: {ToolName}", toolName);
            var toolResult = await _mcpClientService.CallToolAsync(toolName, argumentsJson, cancellationToken);

            conversation.Add(new Dictionary<string, object?>
            {
                ["role"] = "tool",
                ["tool_call_id"] = toolCallId,
                ["content"] = toolResult
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
        using var document = JsonDocument.Parse(body);

        if (document.RootElement.TryGetProperty("data", out var dataElement) &&
            dataElement.ValueKind == JsonValueKind.Array &&
            dataElement.GetArrayLength() > 0)
        {
            var firstModel = dataElement[0];
            if (firstModel.TryGetProperty("id", out var idElement))
            {
                var modelId = idElement.GetString();
                if (!string.IsNullOrWhiteSpace(modelId))
                {
                    _logger.LogInformation("Resolved LM Studio model id {ModelId} from {Path}", modelId, _options.LlmHealthPath);
                    return modelId;
                }
            }
        }

        throw new InvalidOperationException("Could not resolve an LM Studio model id from /v1/models. Set LLM_MODEL explicitly in .env.");
    }

    private string ExtractContent(JsonElement root, string fallbackBody)
    {
        if (root.TryGetProperty("choices", out var choicesElement) &&
            choicesElement.ValueKind == JsonValueKind.Array &&
            choicesElement.GetArrayLength() > 0)
        {
            var firstChoice = choicesElement[0];
            if (firstChoice.TryGetProperty("message", out var messageElement) &&
                messageElement.TryGetProperty("content", out var messageContent))
            {
                if (messageContent.ValueKind == JsonValueKind.String)
                {
                    return messageContent.GetString() ?? string.Empty;
                }

                return messageContent.GetRawText();
            }

            if (firstChoice.TryGetProperty("text", out var textElement))
            {
                return textElement.GetString() ?? string.Empty;
            }
        }

        return fallbackBody;
    }
}
