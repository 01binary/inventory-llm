namespace InventoryDemo.Server.Options;

public sealed class ModelServiceOptions
{
    public const string SectionName = "ModelServices";

    public string LlmBaseUrl { get; set; } = "http://host.docker.internal:1234";
    public string LlmModel { get; set; } = "auto";
    public string WhisperBaseUrl { get; set; } = "http://stt:8080";
    public string McpServerUrl { get; set; } = "http://localhost:8080/mcp";
    public string LlmCompletionPath { get; set; } = "/v1/chat/completions";
    public string LlmHealthPath { get; set; } = "/v1/models";
    public string WhisperTranscriptionPath { get; set; } = "/inference";
    public string WhisperHealthPath { get; set; } = "/";
}
