using System.Text.Json;
using Microsoft.Extensions.Options;
using InventoryDemo.Server.DTOs;
using InventoryDemo.Server.Options;

namespace InventoryDemo.Server.Services;

public sealed class FewShotPromptService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FewShotPromptService> _logger;
    private readonly string _configuredPath;
    private IReadOnlyList<ChatMessageDto>? _cachedPrompts;

    public FewShotPromptService(
        IWebHostEnvironment environment,
        IOptions<AppPathsOptions> paths,
        ILogger<FewShotPromptService> logger)
    {
        _environment = environment;
        _logger = logger;
        _configuredPath = paths.Value.FewShotPromptsPath;
    }

    public IReadOnlyList<ChatMessageDto> GetFewShotPrompts()
    {
        if (_cachedPrompts is not null)
        {
            return _cachedPrompts;
        }

        var resolvedPath = ResolvePath(_configuredPath);
        if (!File.Exists(resolvedPath))
        {
            _logger.LogWarning("Few-shot prompts file not found at {Path}. Continuing with no few-shot prompts.", resolvedPath);
            _cachedPrompts = Array.Empty<ChatMessageDto>();
            return _cachedPrompts;
        }

        try
        {
            var fileContents = File.ReadAllText(resolvedPath);
            var parsed = JsonSerializer.Deserialize<List<ChatMessageDto>>(fileContents);

            _cachedPrompts = parsed?
                .Where(message => !string.IsNullOrWhiteSpace(message.Role) && !string.IsNullOrWhiteSpace(message.Content))
                .Select(message => new ChatMessageDto
                {
                    Role = message.Role.Trim().ToLowerInvariant(),
                    Content = message.Content.Trim()
                })
                .ToList() ?? Array.Empty<ChatMessageDto>();

            _logger.LogInformation("Loaded {Count} few-shot prompt messages from {Path}", _cachedPrompts.Count, resolvedPath);
            return _cachedPrompts;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Few-shot prompts file at {Path} is invalid JSON. Continuing with no few-shot prompts.", resolvedPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read few-shot prompts file at {Path}. Continuing with no few-shot prompts.", resolvedPath);
        }

        _cachedPrompts = Array.Empty<ChatMessageDto>();
        return _cachedPrompts;
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
