using System.Diagnostics;
using Microsoft.Extensions.Options;
using InventoryDemo.Server.Options;

namespace InventoryDemo.Server.Services;

public sealed class TextToSpeechService
{
    private readonly AppPathsOptions _paths;
    private readonly ILogger<TextToSpeechService> _logger;

    public TextToSpeechService(IOptions<AppPathsOptions> paths, ILogger<TextToSpeechService> logger)
    {
        _paths = paths.Value;
        _logger = logger;
    }

    public async Task<byte[]> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.TempAudioDirectory);
        var outputPath = Path.Combine(_paths.TempAudioDirectory, $"{Guid.NewGuid():N}.wav");

        var psi = new ProcessStartInfo
        {
            FileName = _paths.PiperExecutablePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(_paths.PiperVoiceModelPath);
        psi.ArgumentList.Add("--output_file");
        psi.ArgumentList.Add(outputPath);

        _logger.LogInformation("Starting Piper process {Executable}", _paths.PiperExecutablePath);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Piper process");

        await process.StandardInput.WriteAsync(text);
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();

        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);
        var stderr = await stderrTask;
        var stdout = await stdoutTask;

        _logger.LogInformation("Piper exited with code {ExitCode}. stdout={Stdout} stderr={Stderr}", process.ExitCode, stdout.Trim(), stderr.Trim());

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Piper failed with exit code {process.ExitCode}: {stderr}");
        }

        if (!File.Exists(outputPath))
        {
            throw new FileNotFoundException("Piper did not produce an output WAV file", outputPath);
        }

        try
        {
            return await File.ReadAllBytesAsync(outputPath, cancellationToken);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }
}
