namespace InventoryDemo.Server.Models;

public sealed class InventoryItem
{
    public long Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string CreatedUtc { get; set; } = string.Empty;
    public string UpdatedUtc { get; set; } = string.Empty;
}
