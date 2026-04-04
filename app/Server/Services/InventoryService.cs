using System.Data;
using Dapper;
using InventoryDemo.Server.Data;
using InventoryDemo.Server.DTOs;
using InventoryDemo.Server.Models;

namespace InventoryDemo.Server.Services;

public sealed class InventoryService
{
    private readonly DatabaseInitializer _databaseInitializer;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(DatabaseInitializer databaseInitializer, ILogger<InventoryService> logger)
    {
        _databaseInitializer = databaseInitializer;
        _logger = logger;
    }

    public async Task<IReadOnlyList<InventoryItem>> GetItemsAsync()
    {
        const string sql = """
            SELECT id, sku, name, quantity, created_utc AS CreatedUtc, updated_utc AS UpdatedUtc
            FROM items
            ORDER BY name;
            """;

        await using var connection = _databaseInitializer.CreateConnection();
        var items = await connection.QueryAsync<InventoryItem>(sql);
        return items.ToList();
    }

    public async Task<IReadOnlyList<InventoryItem>> SearchItemsByNameOrSkuAsync(string query, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<InventoryItem>();
        }

        var safeLimit = Math.Clamp(limit, 1, 200);
        var escapedQuery = EscapeLikePattern(query.Trim());
        var pattern = $"%{escapedQuery}%";

        const string sql = """
            SELECT id, sku, name, quantity, created_utc AS CreatedUtc, updated_utc AS UpdatedUtc
            FROM items
            WHERE name LIKE @Pattern ESCAPE '\'
               OR sku LIKE @Pattern ESCAPE '\'
            ORDER BY
                CASE
                    WHEN lower(name) = lower(@Exact) THEN 0
                    WHEN lower(sku) = lower(@Exact) THEN 0
                    ELSE 1
                END,
                name
            LIMIT @Limit;
            """;

        await using var connection = _databaseInitializer.CreateConnection();
        var results = await connection.QueryAsync<InventoryItem>(
            sql,
            new
            {
                Pattern = pattern,
                Exact = query.Trim(),
                Limit = safeLimit
            });

        return results.ToList();
    }

    public async Task<InventoryItem?> GetItemAsync(long id)
    {
        const string sql = """
            SELECT id, sku, name, quantity, created_utc AS CreatedUtc, updated_utc AS UpdatedUtc
            FROM items
            WHERE id = @Id;
            """;

        await using var connection = _databaseInitializer.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<InventoryItem>(sql, new { Id = id });
    }

    public async Task<IReadOnlyList<InventoryTransaction>> GetTransactionsAsync()
    {
        const string sql = """
            SELECT t.id,
                   t.item_id AS ItemId,
                   t.transaction_type AS TransactionType,
                   t.quantity_delta AS QuantityDelta,
                   t.note,
                   t.created_utc AS CreatedUtc,
                   i.name AS ItemName,
                   i.sku AS ItemSku
            FROM inventory_transactions t
            INNER JOIN items i ON i.id = t.item_id
            ORDER BY t.created_utc DESC, t.id DESC;
            """;

        await using var connection = _databaseInitializer.CreateConnection();
        var transactions = await connection.QueryAsync<InventoryTransaction>(sql);
        return transactions.ToList();
    }

    public async Task<InventoryItem> CreateItemAsync(InventoryItemUpsertRequest request)
    {
        var now = DateTime.UtcNow.ToString("O");

        await using var connection = _databaseInitializer.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string insertItemSql = """
            INSERT INTO items (sku, name, quantity, created_utc, updated_utc)
            VALUES (@Sku, @Name, @Quantity, @CreatedUtc, @UpdatedUtc);
            SELECT last_insert_rowid();
            """;

        var id = await connection.ExecuteScalarAsync<long>(
            insertItemSql,
            new
            {
                request.Sku,
                request.Name,
                request.Quantity,
                CreatedUtc = now,
                UpdatedUtc = now
            },
            transaction);

        if (request.Quantity != 0)
        {
            await InsertTransactionAsync(connection, transaction, id, "seed", request.Quantity, request.TransactionNote ?? "Initial quantity");
        }

        await transaction.CommitAsync();
        _logger.LogInformation("Created inventory item {ItemId} ({Sku})", id, request.Sku);
        return (await GetItemAsync(id))!;
    }

    public async Task<InventoryItem?> UpdateItemAsync(long id, InventoryItemUpsertRequest request)
    {
        var existing = await GetItemAsync(id);
        if (existing is null)
        {
            return null;
        }

        var now = DateTime.UtcNow.ToString("O");
        var quantityDelta = request.Quantity - existing.Quantity;

        await using var connection = _databaseInitializer.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string updateSql = """
            UPDATE items
            SET sku = @Sku,
                name = @Name,
                quantity = @Quantity,
                updated_utc = @UpdatedUtc
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(
            updateSql,
            new
            {
                Id = id,
                request.Sku,
                request.Name,
                request.Quantity,
                UpdatedUtc = now
            },
            transaction);

        if (quantityDelta != 0)
        {
            await InsertTransactionAsync(connection, transaction, id, "adjustment", quantityDelta, request.TransactionNote ?? "Quantity updated");
        }

        await transaction.CommitAsync();
        _logger.LogInformation("Updated inventory item {ItemId} ({Sku})", id, request.Sku);
        return await GetItemAsync(id);
    }

    public async Task<bool> DeleteItemAsync(long id)
    {
        await using var connection = _databaseInitializer.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string deleteTransactionsSql = "DELETE FROM inventory_transactions WHERE item_id = @Id;";
        const string deleteItemSql = "DELETE FROM items WHERE id = @Id;";

        await connection.ExecuteAsync(deleteTransactionsSql, new { Id = id }, transaction);
        var rows = await connection.ExecuteAsync(deleteItemSql, new { Id = id }, transaction);
        await transaction.CommitAsync();

        if (rows > 0)
        {
            _logger.LogInformation("Deleted inventory item {ItemId}", id);
        }

        return rows > 0;
    }

    public async Task<InventoryTransaction?> AddInventoryTransactionAsync(
        long itemId,
        string transactionType,
        int quantityDelta,
        string? note)
    {
        var normalizedType = string.IsNullOrWhiteSpace(transactionType) ? "adjustment" : transactionType.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow.ToString("O");

        await using var connection = _databaseInitializer.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string itemSql = """
            SELECT id, sku, name, quantity, created_utc AS CreatedUtc, updated_utc AS UpdatedUtc
            FROM items
            WHERE id = @Id;
            """;

        var item = await connection.QuerySingleOrDefaultAsync<InventoryItem>(itemSql, new { Id = itemId }, transaction);
        if (item is null)
        {
            await transaction.RollbackAsync();
            return null;
        }

        const string updateItemSql = """
            UPDATE items
            SET quantity = quantity + @QuantityDelta,
                updated_utc = @UpdatedUtc
            WHERE id = @Id;
            """;

        await connection.ExecuteAsync(
            updateItemSql,
            new
            {
                Id = itemId,
                QuantityDelta = quantityDelta,
                UpdatedUtc = now
            },
            transaction);

        var transactionId = await InsertTransactionAsync(
            connection,
            transaction,
            itemId,
            normalizedType,
            quantityDelta,
            note);

        await transaction.CommitAsync();
        _logger.LogInformation(
            "Added inventory transaction {TransactionId} for item {ItemId} ({TransactionType}, delta {QuantityDelta})",
            transactionId,
            itemId,
            normalizedType,
            quantityDelta);

        const string transactionSql = """
            SELECT t.id,
                   t.item_id AS ItemId,
                   t.transaction_type AS TransactionType,
                   t.quantity_delta AS QuantityDelta,
                   t.note,
                   t.created_utc AS CreatedUtc,
                   i.name AS ItemName,
                   i.sku AS ItemSku
            FROM inventory_transactions t
            INNER JOIN items i ON i.id = t.item_id
            WHERE t.id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<InventoryTransaction>(transactionSql, new { Id = transactionId });
    }

    private static async Task<long> InsertTransactionAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        IDbTransaction transaction,
        long itemId,
        string transactionType,
        int quantityDelta,
        string? note)
    {
        const string transactionSql = """
            INSERT INTO inventory_transactions (item_id, transaction_type, quantity_delta, note, created_utc)
            VALUES (@ItemId, @TransactionType, @QuantityDelta, @Note, @CreatedUtc);
            SELECT last_insert_rowid();
            """;

        return await connection.ExecuteScalarAsync<long>(
            transactionSql,
            new
            {
                ItemId = itemId,
                TransactionType = transactionType,
                QuantityDelta = quantityDelta,
                Note = note,
                CreatedUtc = DateTime.UtcNow.ToString("O")
            },
            transaction);
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
