using Dapper;
using Microsoft.Extensions.Options;
using InventoryDemo.Server.Data;
using InventoryDemo.Server.Models;
using InventoryDemo.Server.Options;

namespace InventoryDemo.Server.Services;

public sealed class DiagnosticsService
{
    private readonly DatabaseInitializer _databaseInitializer;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ModelServiceOptions _modelServices;
    private readonly ILogger<DiagnosticsService> _logger;

    public DiagnosticsService(
        DatabaseInitializer databaseInitializer,
        IHttpClientFactory httpClientFactory,
        IOptions<ModelServiceOptions> modelServices,
        ILogger<DiagnosticsService> logger)
    {
        _databaseInitializer = databaseInitializer;
        _httpClientFactory = httpClientFactory;
        _modelServices = modelServices.Value;
        _logger = logger;
    }

    public async Task<DiagnosticsReport> GetReportAsync()
    {
        var report = new DiagnosticsReport
        {
            App = new DiagnosticCheck { IsHealthy = true, Message = "API is running" },
            Database = await CheckDatabaseAsync(),
            Llm = await CheckHttpAsync("llm", _modelServices.LlmHealthPath, "LLM server")
        };

        report.OverallHealthy = report.App.IsHealthy &&
                               report.Database.IsHealthy &&
                               report.Llm.IsHealthy;

        return report;
    }

    private async Task<DiagnosticCheck> CheckDatabaseAsync()
    {
        try
        {
            await using var connection = _databaseInitializer.CreateConnection();
            var value = await connection.ExecuteScalarAsync<long>("SELECT COUNT(1) FROM items;");
            return new DiagnosticCheck { IsHealthy = true, Message = $"SQLite reachable, items={value}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database diagnostic failed");
            return new DiagnosticCheck { IsHealthy = false, Message = ex.Message };
        }
    }

    private async Task<DiagnosticCheck> CheckHttpAsync(string clientName, string path, string serviceName)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(clientName);
            using var response = await client.GetAsync(path);
            var message = $"{serviceName} responded with HTTP {(int)response.StatusCode}";
            return new DiagnosticCheck { IsHealthy = response.IsSuccessStatusCode, Message = message };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ServiceName} diagnostic failed", serviceName);
            return new DiagnosticCheck { IsHealthy = false, Message = ex.Message };
        }
    }

}
