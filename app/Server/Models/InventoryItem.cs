namespace InventoryDemo.Server.Models;

public sealed class InventoryItem
{
    public long Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Quantity { get; set; }
    public string? Location { get; set; }
    public string Unit { get; set; } = "each";
    public string CreatedUtc { get; set; } = string.Empty;
    public string UpdatedUtc { get; set; } = string.Empty;
}
