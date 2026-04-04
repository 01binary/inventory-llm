using Microsoft.Extensions.Options;
using InventoryDemo.Server.Options;

namespace InventoryDemo.Server.Services;

public sealed class SystemPromptService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SystemPromptService> _logger;
    private readonly string _configuredPath;
    private string _cachedPrompt = string.Empty;

    public SystemPromptService(
        IWebHostEnvironment environment,
        IOptions<AppPathsOptions> paths,
        ILogger<SystemPromptService> logger)
    {
        _environment = environment;
        _logger = logger;
        _configuredPath = paths.Value.SystemPromptPath;
    }

    public string GetSystemPrompt()
    {
        if (!string.IsNullOrWhiteSpace(_cachedPrompt))
        {
            return _cachedPrompt;
        }

        var resolvedPath = ResolvePath(_configuredPath);
        if (!File.Exists(resolvedPath))
        {
            _logger.LogWarning("System prompt file not found at {Path}. Continuing with empty prompt.", resolvedPath);
            _cachedPrompt = string.Empty;
            return _cachedPrompt;
        }

        _cachedPrompt = File.ReadAllText(resolvedPath).Trim();
        _logger.LogInformation("Loaded system prompt from {Path}", resolvedPath);
        return _cachedPrompt;
    }

    private string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, path));
    }
}
