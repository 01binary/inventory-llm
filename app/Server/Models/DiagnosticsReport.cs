namespace InventoryDemo.Server.Models;

public sealed class DiagnosticsReport
{
    public string TimestampUtc { get; set; } = DateTime.UtcNow.ToString("O");
    public bool OverallHealthy { get; set; }
    public DiagnosticCheck App { get; set; } = new();
    public DiagnosticCheck Database { get; set; } = new();
    public DiagnosticCheck Llm { get; set; } = new();
    public DiagnosticCheck Stt { get; set; } = new();
    public DiagnosticCheck PiperExecutable { get; set; } = new();
    public DiagnosticCheck PiperVoiceModel { get; set; } = new();
}

public sealed class DiagnosticCheck
{
    public bool IsHealthy { get; set; }
    public string Message { get; set; } = string.Empty;
}
