using Microsoft.AspNetCore.Mvc;
using InventoryDemo.Server.Services;

namespace InventoryDemo.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ConfigController : ControllerBase
{
    private readonly AppConfigService _configService;

    public ConfigController(AppConfigService configService)
    {
        _configService = configService;
    }

    [HttpGet]
    public IActionResult Get() => Ok(_configService.GetPublicConfig());
}
