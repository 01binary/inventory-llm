using System.Data;
using System.Globalization;
using System.Text;
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
        var normalizedQuery = NormalizeForSearch(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Array.Empty<InventoryItem>();
        }

        var items = await GetItemsAsync();
        return items
            .Select(item => new
            {
                Item = item,
                Score = Math.Max(
                    ComputeSearchScore(normalizedQuery, item.Name),
                    ComputeSearchScore(normalizedQuery, item.Sku))
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(safeLimit)
            .Select(candidate => candidate.Item)
            .ToList();
    }

    public static bool AreEquivalentForSearch(string left, string right)
    {
        return string.Equals(
            NormalizeForSearch(left),
            NormalizeForSearch(right),
            StringComparison.Ordinal);
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

    public async Task<InventoryItem?> GetItemBySkuAsync(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return null;
        }

        const string sql = """
            SELECT id, sku, name, quantity, created_utc AS CreatedUtc, updated_utc AS UpdatedUtc
            FROM items
            WHERE lower(sku) = lower(@Sku)
            LIMIT 1;
            """;

        await using var connection = _databaseInitializer.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<InventoryItem>(sql, new { Sku = sku.Trim() });
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

    public async Task<(InventoryItem Item, bool Created, bool TransactionApplied)> CreateOrApplyTransactionBySkuAsync(
        InventoryItemUpsertRequest request)
    {
        var existing = await GetItemBySkuAsync(request.Sku);
        if (existing is null)
        {
            var createdItem = await CreateItemAsync(request);
            return (createdItem, true, request.Quantity != 0);
        }

        var desiredName = string.IsNullOrWhiteSpace(request.Name) ? existing.Name : request.Name.Trim();
        if (!string.Equals(existing.Name, desiredName, StringComparison.Ordinal))
        {
            var renamed = await UpdateItemAsync(existing.Id, new InventoryItemUpsertRequest
            {
                Sku = existing.Sku,
                Name = desiredName,
                Quantity = existing.Quantity
            });

            if (renamed is not null)
            {
                existing = renamed;
            }
        }

        var appliedTransaction = false;
        if (request.Quantity != 0)
        {
            await AddInventoryTransactionAsync(
                existing.Id,
                "adjustment",
                request.Quantity,
                string.IsNullOrWhiteSpace(request.TransactionNote) ? "Added from add item form" : request.TransactionNote);
            appliedTransaction = true;
        }

        var refreshed = await GetItemAsync(existing.Id) ?? existing;
        return (refreshed, false, appliedTransaction);
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

    private static int ComputeSearchScore(string query, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return 0;
        }

        var normalizedCandidate = NormalizeForSearch(candidate);
        if (string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return 0;
        }

        if (string.Equals(normalizedCandidate, query, StringComparison.Ordinal))
        {
            return 1000;
        }

        if (normalizedCandidate.StartsWith(query, StringComparison.Ordinal))
        {
            return 900;
        }

        if (normalizedCandidate.Contains(query, StringComparison.Ordinal))
        {
            return 800;
        }

        var queryTokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var candidateTokens = normalizedCandidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (queryTokens.Length > 0)
        {
            var fullTokenMatches = queryTokens.Count(token => candidateTokens.Any(candidateToken =>
                candidateToken.Contains(token, StringComparison.Ordinal) ||
                candidateToken.StartsWith(token, StringComparison.Ordinal)));

            if (fullTokenMatches == queryTokens.Length)
            {
                return 700 + (fullTokenMatches * 10);
            }
        }

        var overallDistance = LevenshteinDistance(query, normalizedCandidate);
        var maxOverallDistance = Math.Max(1, query.Length / 3);
        if (overallDistance <= maxOverallDistance)
        {
            return 600 - (overallDistance * 10);
        }

        if (queryTokens.Length == 1)
        {
            var queryToken = queryTokens[0];
            var bestTokenDistance = candidateTokens
                .Select(token => LevenshteinDistance(queryToken, token))
                .DefaultIfEmpty(int.MaxValue)
                .Min();

            var maxTokenDistance = Math.Max(1, queryToken.Length / 3);
            if (bestTokenDistance <= maxTokenDistance)
            {
                return 500 - (bestTokenDistance * 10);
            }
        }

        return 0;
    }

    private static string NormalizeForSearch(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var formD = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);
        var previousWasSpace = false;
        foreach (var ch in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                previousWasSpace = false;
                continue;
            }

            if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a))
        {
            return b.Length;
        }

        if (string.IsNullOrEmpty(b))
        {
            return a.Length;
        }

        var costs = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            costs[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            var previousDiagonal = costs[0];
            costs[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var temp = costs[j];
                var substitutionCost = a[i - 1] == b[j - 1] ? 0 : 1;
                costs[j] = Math.Min(
                    Math.Min(costs[j] + 1, costs[j - 1] + 1),
                    previousDiagonal + substitutionCost);
                previousDiagonal = temp;
            }
        }

        return costs[b.Length];
    }
}
