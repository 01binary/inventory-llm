using Microsoft.AspNetCore.Mvc;
using InventoryDemo.Server.Services;

namespace InventoryDemo.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    private readonly DiagnosticsService _diagnosticsService;

    public HealthController(DiagnosticsService diagnosticsService)
    {
        _diagnosticsService = diagnosticsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        var report = await _diagnosticsService.GetReportAsync();
        return report.OverallHealthy ? Ok(report) : StatusCode(StatusCodes.Status503ServiceUnavailable, report);
    }

    [HttpGet("/api/diagnostics")]
    public Task<IActionResult> GetDiagnosticsAsync() => GetAsync();
}
