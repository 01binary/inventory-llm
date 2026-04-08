namespace InventoryDemo.Server.Options;

public sealed class AppPathsOptions
{
    public const string SectionName = "AppPaths";

    public string DatabasePath { get; set; } = "/data/inventory.db";
    public string SqlScriptsPath { get; set; } = "db";
    public string TempAudioDirectory { get; set; } = "/tmp/inventory-audio";
    public string FfmpegExecutablePath { get; set; } = "/usr/bin/ffmpeg";
    public string PiperExecutablePath { get; set; } = "/usr/local/bin/piper";
    public string PiperVoiceModelPath { get; set; } = "/models/piper/es_MX-claude-high.onnx";
    public string SystemPromptPath { get; set; } = "SYSTEM_PROMPT.md";
    public string FewShotPromptsPath { get; set; } = "FEW_SHOT_PROMPTS.json";
}
