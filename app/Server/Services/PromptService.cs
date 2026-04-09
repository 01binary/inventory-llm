using System.Text.Json;
using Microsoft.Extensions.Options;
using InventoryDemo.Server.DTOs;
using InventoryDemo.Server.Options;

namespace InventoryDemo.Server.Services;

public sealed class PromptService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PromptService> _logger;
    private readonly string _systemPromptPath;
    private readonly string _helloPromptPath;
    private readonly string _fewShotPromptsPath;

    private string? _cachedSystemPrompt;
    private string? _cachedHelloPrompt;
    private IReadOnlyList<ChatMessageDto>? _cachedFewShotPrompts;

    public PromptService(
        IWebHostEnvironment environment,
        IOptions<AppPathsOptions> paths,
        ILogger<PromptService> logger)
    {
        _environment = environment;
        _logger = logger;
        _systemPromptPath = paths.Value.SystemPromptPath;
        _helloPromptPath = paths.Value.HelloPromptPath;
        _fewShotPromptsPath = paths.Value.FewShotPromptsPath;
    }

    public string GetSystemPrompt()
    {
        if (_cachedSystemPrompt is not null)
        {
            return _cachedSystemPrompt;
        }

        _cachedSystemPrompt = ReadTextPrompt(_systemPromptPath, "System prompt");
        return _cachedSystemPrompt;
    }

    public string GetHelloPrompt()
    {
        if (_cachedHelloPrompt is not null)
        {
            return _cachedHelloPrompt;
        }

        _cachedHelloPrompt = ReadTextPrompt(_helloPromptPath, "Hello prompt");
        return _cachedHelloPrompt;
    }

    public IReadOnlyList<ChatMessageDto> GetFewShotPrompts()
    {
        if (_cachedFewShotPrompts is not null)
        {
            return _cachedFewShotPrompts;
        }

        var resolvedPath = ResolvePath(_fewShotPromptsPath);
        if (!File.Exists(resolvedPath))
        {
            _logger.LogWarning("Few-shot prompts file not found at {Path}. Continuing with no few-shot prompts.", resolvedPath);
            _cachedFewShotPrompts = Array.Empty<ChatMessageDto>();
            return _cachedFewShotPrompts;
        }

        try
        {
            var fileContents = File.ReadAllText(resolvedPath);
            var parsed = JsonSerializer.Deserialize<List<ChatMessageDto>>(fileContents);

            _cachedFewShotPrompts = parsed?
                .Where(message => !string.IsNullOrWhiteSpace(message.Role) && !string.IsNullOrWhiteSpace(message.Content))
                .Select(message => new ChatMessageDto
                {
                    Role = message.Role.Trim().ToLowerInvariant(),
                    Content = message.Content.Trim()
                })
                .ToList() ?? new List<ChatMessageDto>();

            _logger.LogInformation("Loaded {Count} few-shot prompt messages from {Path}", _cachedFewShotPrompts.Count, resolvedPath);
            return _cachedFewShotPrompts;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Few-shot prompts file at {Path} is invalid JSON. Continuing with no few-shot prompts.", resolvedPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read few-shot prompts file at {Path}. Continuing with no few-shot prompts.", resolvedPath);
        }

        _cachedFewShotPrompts = Array.Empty<ChatMessageDto>();
        return _cachedFewShotPrompts;
    }

    private string ReadTextPrompt(string configuredPath, string label)
    {
        var resolvedPath = ResolvePath(configuredPath);
        if (!File.Exists(resolvedPath))
        {
            _logger.LogWarning("{Label} file not found at {Path}. Continuing with empty prompt.", label, resolvedPath);
            return string.Empty;
        }

        var value = File.ReadAllText(resolvedPath).Trim();
        _logger.LogInformation("Loaded {Label} from {Path}", label.ToLowerInvariant(), resolvedPath);
        return value;
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
