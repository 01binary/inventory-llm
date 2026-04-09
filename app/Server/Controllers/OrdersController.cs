using Microsoft.AspNetCore.Mvc;
using InventoryDemo.Server.DTOs;
using InventoryDemo.Server.Services;

namespace InventoryDemo.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;

    public OrdersController(OrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecentAsync([FromQuery] int limit = 20)
    {
        var orders = await _orderService.GetRecentOrdersAsync(limit);
        return Ok(orders);
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatestAsync()
    {
        var order = await _orderService.GetLatestOrderAsync();
        return order is null ? NotFound(new { message = "No orders found." }) : Ok(order);
    }

    [HttpGet("{orderNumber:long}")]
    public async Task<IActionResult> GetByOrderNumberAsync(long orderNumber)
    {
        var order = await _orderService.GetOrderAsync(orderNumber);
        return order is null ? NotFound(new { message = $"Order {orderNumber} was not found." }) : Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] OrderItemsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var order = await _orderService.CreateOrderAsync(request.Items);
            return CreatedAtAction(nameof(GetByOrderNumberAsync), new { orderNumber = order.OrderNumber }, order);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("latest/items")]
    public async Task<IActionResult> AddItemsToLatestAsync([FromBody] OrderItemsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var updated = await _orderService.AddItemsToLatestOrderAsync(request.Items);
            if (updated is null)
            {
                return NotFound(new { message = "No existing order to update. Create an order first." });
            }

            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
