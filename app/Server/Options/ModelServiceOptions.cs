namespace InventoryDemo.Server.Options;

public sealed class ModelServiceOptions
{
    public const string SectionName = "ModelServices";

    public string LlamaProvider { get; set; } = "OpenAICompatible";
    public string LlamaBaseUrl { get; set; } = "http://host.docker.internal:1234";
    public string LlamaModel { get; set; } = "auto";
    public string WhisperBaseUrl { get; set; } = "http://stt:8080";
    public string LlamaCompletionPath { get; set; } = "/v1/chat/completions";
    public string LlamaHealthPath { get; set; } = "/v1/models";
    public string WhisperTranscriptionPath { get; set; } = "/inference";
    public string WhisperHealthPath { get; set; } = "/";
}
