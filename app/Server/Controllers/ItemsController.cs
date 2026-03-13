using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using InventoryDemo.Server.DTOs;
using InventoryDemo.Server.Services;

namespace InventoryDemo.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ItemsController : ControllerBase
{
    private readonly InventoryService _inventoryService;
    private readonly ILogger<ItemsController> _logger;

    public ItemsController(InventoryService inventoryService, ILogger<ItemsController> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllAsync() => Ok(await _inventoryService.GetItemsAsync());

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetByIdAsync(long id)
    {
        var item = await _inventoryService.GetItemAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] InventoryItemUpsertRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var created = await _inventoryService.CreateItemAsync(request);
            return CreatedAtAction(nameof(GetByIdAsync), new { id = created.Id }, created);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            _logger.LogWarning(ex, "Attempted to create duplicate SKU {Sku}", request.Sku);
            return Conflict(new { message = "An item with that SKU already exists." });
        }
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateAsync(long id, [FromBody] InventoryItemUpsertRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var updated = await _inventoryService.UpdateItemAsync(id, request);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            _logger.LogWarning(ex, "Attempted to update item {ItemId} with duplicate SKU {Sku}", id, request.Sku);
            return Conflict(new { message = "An item with that SKU already exists." });
        }
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAsync(long id)
    {
        var deleted = await _inventoryService.DeleteItemAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
