using System.ComponentModel.DataAnnotations;

namespace InventoryDemo.Server.DTOs;

public sealed class InventoryItemUpsertRequest
{
    [Required]
    [MaxLength(64)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }

    [MaxLength(400)]
    public string? TransactionNote { get; set; }
}
