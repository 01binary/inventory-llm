namespace InventoryDemo.Server.Options;

public sealed class AppPathsOptions
{
    public const string SectionName = "AppPaths";

    public string DatabasePath { get; set; } = "/data/inventory.db";
    public string SqlScriptsPath { get; set; } = "db";
    public string SystemPromptPath { get; set; } = "SYSTEM_PROMPT.md";
    public string HelloPromptPath { get; set; } = "HELLO_PROMPT.md";
    public string FewShotPromptsPath { get; set; } = "FEW_SHOT_PROMPTS.json";
}
