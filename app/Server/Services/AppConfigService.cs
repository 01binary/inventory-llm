using Microsoft.Extensions.Options;
using InventoryDemo.Server.Options;

namespace InventoryDemo.Server.Services;

public sealed class AppConfigService
{
    private readonly AppPathsOptions _paths;
    private readonly ModelServiceOptions _models;
    private readonly SystemPromptService _systemPromptService;

    public AppConfigService(
        IOptions<AppPathsOptions> paths,
        IOptions<ModelServiceOptions> models,
        SystemPromptService systemPromptService)
    {
        _paths = paths.Value;
        _models = models.Value;
        _systemPromptService = systemPromptService;
    }

    public object GetPublicConfig() => new
    {
        databasePath = _paths.DatabasePath,
        tempAudioDirectory = _paths.TempAudioDirectory,
        llmBaseUrl = _models.LlmBaseUrl,
        llmModel = _models.LlmModel,
        whisperBaseUrl = _models.WhisperBaseUrl,
        mcpServerUrl = _models.McpServerUrl,
        piperExecutablePath = _paths.PiperExecutablePath,
        piperVoiceModelPath = _paths.PiperVoiceModelPath,
        hasSystemPrompt = !string.IsNullOrWhiteSpace(_systemPromptService.GetSystemPrompt())
    };
}
