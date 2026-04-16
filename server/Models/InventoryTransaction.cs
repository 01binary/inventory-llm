namespace InventoryDemo.Server.Models;

public sealed class InventoryTransaction
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public int QuantityDelta { get; set; }
    public string? Note { get; set; }
    public string CreatedUtc { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public string? ItemSku { get; set; }
}
