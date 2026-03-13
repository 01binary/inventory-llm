using System.Text.Json;
using Microsoft.Extensions.Options;
using InventoryDemo.Server.Options;

namespace InventoryDemo.Server.Services;

public sealed class SpeechToTextProxyService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ModelServiceOptions _options;
    private readonly ILogger<SpeechToTextProxyService> _logger;

    public SpeechToTextProxyService(
        IHttpClientFactory httpClientFactory,
        IOptions<ModelServiceOptions> options,
        ILogger<SpeechToTextProxyService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<object> TranscribeAsync(IFormFile audioFile)
    {
        var client = _httpClientFactory.CreateClient("stt");
        await using var fileStream = audioFile.OpenReadStream();
        using var form = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(audioFile.ContentType ?? "application/octet-stream");
        form.Add(streamContent, "file", audioFile.FileName);

        _logger.LogInformation("Forwarding audio file {FileName} ({Length} bytes) to whisper.cpp at {Path}", audioFile.FileName, audioFile.Length, _options.WhisperTranscriptionPath);
        using var response = await client.PostAsync(_options.WhisperTranscriptionPath, form);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("whisper.cpp returned {StatusCode}: {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"whisper.cpp request failed with HTTP {(int)response.StatusCode}");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var text = root.TryGetProperty("text", out var textElement) ? textElement.GetString() : body;
            return new { text = text?.Trim() ?? string.Empty, raw = JsonSerializer.Deserialize<object>(body) };
        }
        catch (JsonException)
        {
            return new { text = body.Trim(), raw = body };
        }
    }
}
