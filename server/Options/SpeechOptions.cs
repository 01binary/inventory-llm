namespace InventoryDemo.Server.Options;

public sealed class SpeechOptions
{
    public const string SectionName = "Speech";

    public string DefaultSttLanguage { get; set; } = "en-US";
    public string DefaultTtsLanguage { get; set; } = "en-US";
    public string PreferredTtsVoiceName { get; set; } = "Google US English";
}
