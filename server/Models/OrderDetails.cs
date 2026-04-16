namespace InventoryDemo.Server.Models;

public sealed class OrderDetails
{
    public long OrderNumber { get; set; }

    public string CreatedUtc { get; set; } = string.Empty;

    public string UpdatedUtc { get; set; } = string.Empty;

    public int LineCount { get; set; }

    public int TotalQuantity { get; set; }

    public IReadOnlyList<OrderLine> Items { get; set; } = Array.Empty<OrderLine>();
}
