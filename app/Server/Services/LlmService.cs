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
        var payload = new
        {
            prompt = request.Prompt,
            n_predict = request.MaxTokens,
            temperature = 0.2,
            stop = new[] { "</s>" }
        };

        var json = JsonSerializer.Serialize(payload);
        _logger.LogInformation("Sending completion request to llama.cpp at {Path}", _options.LlamaCompletionPath);

        using var response = await client.PostAsync(
            _options.LlamaCompletionPath,
            new StringContent(json, Encoding.UTF8, "application/json"));

        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("llama.cpp returned {StatusCode}: {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"llama.cpp request failed with HTTP {(int)response.StatusCode}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var content = root.TryGetProperty("content", out var contentElement)
            ? contentElement.GetString()
            : root.TryGetProperty("response", out var responseElement)
                ? responseElement.GetString()
                : body;

        return new
        {
            text = content?.Trim() ?? string.Empty,
            raw = JsonSerializer.Deserialize<object>(body)
        };
    }
}
