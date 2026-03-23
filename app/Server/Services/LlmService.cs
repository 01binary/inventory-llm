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
    private readonly ILogger<LlmService> _logger;

    public LlmService(
        IHttpClientFactory httpClientFactory,
        IOptions<ModelServiceOptions> options,
        ILogger<LlmService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<object> CompleteAsync(ChatCompletionRequest request)
    {
        var client = _httpClientFactory.CreateClient("llm");
        var payload = await BuildPayloadAsync(request);
        var json = JsonSerializer.Serialize(payload);
        _logger.LogInformation("Sending completion request to LLM server at {Path}", _options.LlmCompletionPath);

        using var response = await client.PostAsync(
            _options.LlmCompletionPath,
            new StringContent(json, Encoding.UTF8, "application/json"));

        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("LLM server returned {StatusCode}: {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"LLM request failed with HTTP {(int)response.StatusCode}");
        }

        using var document = JsonDocument.Parse(body);
        var content = ExtractContent(document.RootElement, body);

        return new
        {
            text = content?.Trim() ?? string.Empty,
            raw = JsonSerializer.Deserialize<object>(body)
        };
    }

    private async Task<object> BuildPayloadAsync(ChatCompletionRequest request)
    {
        return new
        {
            model = await ResolveModelAsync(),
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = request.Prompt
                }
            },
            temperature = 0.2,
            max_tokens = request.MaxTokens,
            stream = false
        };
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
                return messageContent.GetString() ?? string.Empty;
            }

            if (firstChoice.TryGetProperty("text", out var textElement))
            {
                return textElement.GetString() ?? string.Empty;
            }
        }

        return fallbackBody;
    }
}
