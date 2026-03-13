namespace InventoryDemo.Server.Options;

public sealed class ModelServiceOptions
{
    public const string SectionName = "ModelServices";

    public string LlamaBaseUrl { get; set; } = "http://llm:8080";
    public string WhisperBaseUrl { get; set; } = "http://stt:8080";
    public string LlamaCompletionPath { get; set; } = "/completion";
    public string LlamaHealthPath { get; set; } = "/health";
    public string WhisperTranscriptionPath { get; set; } = "/inference";
    public string WhisperHealthPath { get; set; } = "/";
}
