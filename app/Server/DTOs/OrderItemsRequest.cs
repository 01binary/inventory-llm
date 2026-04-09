using System.ComponentModel.DataAnnotations;

namespace InventoryDemo.Server.DTOs;

public sealed class OrderItemsRequest
{
    [Required]
    [MinLength(1)]
    public List<OrderItemRequest> Items { get; set; } = [];
}
