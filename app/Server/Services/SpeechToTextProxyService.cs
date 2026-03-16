using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using InventoryDemo.Server.Options;

namespace InventoryDemo.Server.Services;

public sealed class SpeechToTextProxyService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppPathsOptions _paths;
    private readonly ModelServiceOptions _options;
    private readonly ILogger<SpeechToTextProxyService> _logger;

    public SpeechToTextProxyService(
        IHttpClientFactory httpClientFactory,
        IOptions<AppPathsOptions> paths,
        IOptions<ModelServiceOptions> options,
        ILogger<SpeechToTextProxyService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _paths = paths.Value;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<object> TranscribeAsync(IFormFile audioFile)
    {
        Directory.CreateDirectory(_paths.TempAudioDirectory);
        var sourceExtension = Path.GetExtension(audioFile.FileName);
        if (string.IsNullOrWhiteSpace(sourceExtension))
        {
            sourceExtension = ".bin";
        }

        var sourcePath = Path.Combine(_paths.TempAudioDirectory, $"{Guid.NewGuid():N}{sourceExtension}");
        var wavPath = Path.Combine(_paths.TempAudioDirectory, $"{Guid.NewGuid():N}.wav");

        await using (var destination = File.Create(sourcePath))
        {
            await audioFile.CopyToAsync(destination);
        }

        var client = _httpClientFactory.CreateClient("stt");
        await ConvertToWavAsync(sourcePath, wavPath);

        await using var fileStream = File.OpenRead(wavPath);
        using var form = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        form.Add(streamContent, "file", "recording.wav");
        form.Add(new StringContent("es"), "language");

        _logger.LogInformation(
            "Forwarding transcoded audio file {FileName} ({Length} bytes) to whisper.cpp at {Path}",
            audioFile.FileName,
            new FileInfo(wavPath).Length,
            _options.WhisperTranscriptionPath);

        try
        {
            using var response = await client.PostAsync(_options.WhisperTranscriptionPath, form);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("whisper.cpp returned {StatusCode}: {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"whisper.cpp request failed with HTTP {(int)response.StatusCode}");
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var errorElement))
            {
                var error = errorElement.GetString() ?? "Unknown whisper.cpp error";
                _logger.LogWarning("whisper.cpp returned application error: {Error}", error);
                throw new InvalidOperationException(error);
            }

            var text = root.TryGetProperty("text", out var textElement) ? textElement.GetString() : body;
            return new { text = text?.Trim() ?? string.Empty, raw = JsonSerializer.Deserialize<object>(body) };
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("whisper.cpp returned a non-JSON response.");
        }
        finally
        {
            if (File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }

            if (File.Exists(wavPath))
            {
                File.Delete(wavPath);
            }
        }
    }

    private async Task ConvertToWavAsync(string sourcePath, string wavPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _paths.FfmpegExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(sourcePath);
        psi.ArgumentList.Add("-ar");
        psi.ArgumentList.Add("16000");
        psi.ArgumentList.Add("-ac");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("-c:a");
        psi.ArgumentList.Add("pcm_s16le");
        psi.ArgumentList.Add(wavPath);

        _logger.LogInformation("Converting uploaded audio to WAV using ffmpeg at {Executable}", _paths.FfmpegExecutablePath);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ffmpeg");
        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stderr = await stderrTask;
        var stdout = await stdoutTask;

        _logger.LogInformation("ffmpeg exited with code {ExitCode}. stdout={Stdout} stderr={Stderr}", process.ExitCode, stdout.Trim(), stderr.Trim());

        if (process.ExitCode != 0 || !File.Exists(wavPath))
        {
            throw new InvalidOperationException($"ffmpeg failed to convert uploaded audio: {stderr}");
        }
    }
}
