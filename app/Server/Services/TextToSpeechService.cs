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
        var tempAudioDirectory = ResolvePath(_paths.TempAudioDirectory);
        var voiceModelPath = ResolvePath(_paths.PiperVoiceModelPath);
        var piperExecutable = ResolveExecutablePath(_paths.PiperExecutablePath);

        if (!File.Exists(voiceModelPath))
        {
            throw new InvalidOperationException($"Piper voice model file was not found at '{voiceModelPath}'.");
        }

        Directory.CreateDirectory(tempAudioDirectory);
        var outputPath = Path.Combine(tempAudioDirectory, $"{Guid.NewGuid():N}.wav");

        var psi = new ProcessStartInfo
        {
            FileName = piperExecutable,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(voiceModelPath);
        psi.ArgumentList.Add("--output_file");
        psi.ArgumentList.Add(outputPath);

        _logger.LogInformation("Starting Piper process {Executable}", piperExecutable);
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

    private static string ResolvePath(string configuredPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(configuredPath, Directory.GetCurrentDirectory());
    }

    private static string ResolveExecutablePath(string configuredPath)
    {
        var hasDirectory = configuredPath.Contains('/') || configuredPath.Contains('\\');
        if (!hasDirectory)
        {
            return configuredPath;
        }

        var resolved = ResolvePath(configuredPath);
        if (!File.Exists(resolved))
        {
            throw new InvalidOperationException($"Piper executable was not found at '{resolved}'.");
        }

        return resolved;
    }
}
