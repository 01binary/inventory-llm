namespace InventoryDemo.Server.Options;

public sealed class SpeechOptions
{
    public const string SectionName = "Speech";

    public string DefaultSttLanguage { get; set; } = "es-MX";
    public string DefaultTtsLanguage { get; set; } = "es-MX";
    public string PreferredTtsVoiceName { get; set; } = "Google español de Estados Unidos";
}
