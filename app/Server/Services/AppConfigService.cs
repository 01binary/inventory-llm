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
        llmBaseUrl = _models.LlmBaseUrl,
        llmModel = _models.LlmModel,
        mcpServerUrl = _models.McpServerUrl,
        hasSystemPrompt = !string.IsNullOrWhiteSpace(_systemPromptService.GetSystemPrompt())
    };
}
