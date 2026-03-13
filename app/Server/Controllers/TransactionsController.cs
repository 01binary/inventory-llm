using Microsoft.AspNetCore.Mvc;
using InventoryDemo.Server.Services;

namespace InventoryDemo.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TransactionsController : ControllerBase
{
    private readonly InventoryService _inventoryService;

    public TransactionsController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync() => Ok(await _inventoryService.GetTransactionsAsync());
}
