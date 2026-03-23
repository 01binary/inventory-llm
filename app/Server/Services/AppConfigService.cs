using Microsoft.Extensions.Options;
using InventoryDemo.Server.Options;

namespace InventoryDemo.Server.Services;

public sealed class AppConfigService
{
    private readonly AppPathsOptions _paths;
    private readonly ModelServiceOptions _models;

    public AppConfigService(IOptions<AppPathsOptions> paths, IOptions<ModelServiceOptions> models)
    {
        _paths = paths.Value;
        _models = models.Value;
    }

    public object GetPublicConfig() => new
    {
        databasePath = _paths.DatabasePath,
        tempAudioDirectory = _paths.TempAudioDirectory,
        llmBaseUrl = _models.LlmBaseUrl,
        llmModel = _models.LlmModel,
        whisperBaseUrl = _models.WhisperBaseUrl,
        piperExecutablePath = _paths.PiperExecutablePath,
        piperVoiceModelPath = _paths.PiperVoiceModelPath
    };
}
