namespace InventoryDemo.Server.Models;

public sealed class OrderLine
{
    public long Id { get; set; }

    public long OrderNumber { get; set; }

    public string Sku { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string? ItemName { get; set; }

    public string CreatedUtc { get; set; } = string.Empty;

    public string UpdatedUtc { get; set; } = string.Empty;
}
