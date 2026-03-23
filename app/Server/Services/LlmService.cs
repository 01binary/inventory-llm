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
        _logger.LogInformation("Sending completion request to {Provider} at {Path}", _options.LlamaProvider, _options.LlamaCompletionPath);

        using var response = await client.PostAsync(
            _options.LlamaCompletionPath,
            new StringContent(json, Encoding.UTF8, "application/json"));

        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("LLM provider {Provider} returned {StatusCode}: {Body}", _options.LlamaProvider, response.StatusCode, body);
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
        if (string.Equals(_options.LlamaProvider, "LlamaCpp", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                prompt = request.Prompt,
                n_predict = request.MaxTokens,
                temperature = 0.2,
                stop = new[] { "</s>" }
            };
        }

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
        if (!string.Equals(_options.LlamaProvider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase))
        {
            return _options.LlamaModel;
        }

        if (!string.IsNullOrWhiteSpace(_options.LlamaModel) &&
            !string.Equals(_options.LlamaModel, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return _options.LlamaModel;
        }

        var client = _httpClientFactory.CreateClient("llm");
        using var response = await client.GetAsync(_options.LlamaHealthPath);
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
                    _logger.LogInformation("Resolved LM Studio model id {ModelId} from {Path}", modelId, _options.LlamaHealthPath);
                    return modelId;
                }
            }
        }

        throw new InvalidOperationException("Could not resolve an LM Studio model id from /v1/models. Set LLM_MODEL explicitly in .env.");
    }

    private string ExtractContent(JsonElement root, string fallbackBody)
    {
        if (string.Equals(_options.LlamaProvider, "LlamaCpp", StringComparison.OrdinalIgnoreCase))
        {
            return root.TryGetProperty("content", out var contentElement)
                ? contentElement.GetString() ?? string.Empty
                : root.TryGetProperty("response", out var responseElement)
                    ? responseElement.GetString() ?? string.Empty
                    : fallbackBody;
        }

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
