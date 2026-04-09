using Dapper;
using System.Data;
using InventoryDemo.Server.Data;
using InventoryDemo.Server.DTOs;
using InventoryDemo.Server.Models;

namespace InventoryDemo.Server.Services;

public sealed class OrderService
{
    private readonly DatabaseInitializer _databaseInitializer;
    private readonly ILogger<OrderService> _logger;

    public OrderService(DatabaseInitializer databaseInitializer, ILogger<OrderService> logger)
    {
        _databaseInitializer = databaseInitializer;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OrderDetails>> GetRecentOrdersAsync(int limit = 20)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);

        const string sql = """
            SELECT order_number
            FROM orders
            GROUP BY order_number
            ORDER BY order_number DESC
            LIMIT @Limit;
            """;

        await using var connection = _databaseInitializer.CreateConnection();
        var orderNumbers = (await connection.QueryAsync<long>(sql, new { Limit = safeLimit })).ToList();

        var results = new List<OrderDetails>(orderNumbers.Count);
        foreach (var orderNumber in orderNumbers)
        {
            var order = await GetOrderAsync(orderNumber);
            if (order is not null)
            {
                results.Add(order);
            }
        }

        return results;
    }

    public async Task<OrderDetails?> GetLatestOrderAsync()
    {
        const string sql = """
            SELECT order_number
            FROM orders
            ORDER BY order_number DESC
            LIMIT 1;
            """;

        await using var connection = _databaseInitializer.CreateConnection();
        var latestOrderNumber = await connection.QuerySingleOrDefaultAsync<long?>(sql);
        return latestOrderNumber is null ? null : await GetOrderAsync(latestOrderNumber.Value);
    }

    public async Task<OrderDetails?> GetOrderAsync(long orderNumber)
    {
        const string sql = """
            SELECT o.id,
                   o.order_number AS OrderNumber,
                   o.sku,
                   o.quantity,
                   o.created_utc AS CreatedUtc,
                   o.updated_utc AS UpdatedUtc,
                   i.name AS ItemName
            FROM orders o
            LEFT JOIN items i ON lower(i.sku) = lower(o.sku)
            WHERE o.order_number = @OrderNumber
            ORDER BY o.sku COLLATE NOCASE, o.id;
            """;

        await using var connection = _databaseInitializer.CreateConnection();
        var lines = (await connection.QueryAsync<OrderLine>(sql, new { OrderNumber = orderNumber })).ToList();
        if (lines.Count == 0)
        {
            return null;
        }

        return BuildOrderDetails(orderNumber, lines);
    }

    public async Task<OrderDetails> CreateOrderAsync(IReadOnlyList<OrderItemRequest> items)
    {
        var normalizedItems = NormalizeItems(items);
        var now = DateTime.UtcNow.ToString("O");

        await using var connection = _databaseInitializer.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string nextOrderSql = """
            SELECT COALESCE(MAX(order_number), 0) + 1
            FROM orders;
            """;

        var nextOrderNumber = await connection.ExecuteScalarAsync<long>(nextOrderSql, transaction: transaction);

        foreach (var item in normalizedItems)
        {
            await AddOrIncrementLineAsync(connection, transaction, nextOrderNumber, item.Sku, item.Quantity, now);
        }

        await transaction.CommitAsync();
        _logger.LogInformation("Created order {OrderNumber} with {LineCount} lines", nextOrderNumber, normalizedItems.Count);

        return (await GetOrderAsync(nextOrderNumber))!;
    }

    public async Task<OrderDetails?> AddItemsToLatestOrderAsync(IReadOnlyList<OrderItemRequest> items)
    {
        var normalizedItems = NormalizeItems(items);
        var now = DateTime.UtcNow.ToString("O");

        await using var connection = _databaseInitializer.CreateConnection();
        await connection.OpenAsync();

        const string latestOrderSql = """
            SELECT order_number
            FROM orders
            ORDER BY order_number DESC
            LIMIT 1;
            """;

        var latestOrderNumber = await connection.QuerySingleOrDefaultAsync<long?>(latestOrderSql);
        if (latestOrderNumber is null)
        {
            return null;
        }

        await using var transaction = await connection.BeginTransactionAsync();
        foreach (var item in normalizedItems)
        {
            await AddOrIncrementLineAsync(connection, transaction, latestOrderNumber.Value, item.Sku, item.Quantity, now);
        }

        await transaction.CommitAsync();
        _logger.LogInformation(
            "Added {LineCount} item entries to latest order {OrderNumber}",
            normalizedItems.Count,
            latestOrderNumber.Value);

        return await GetOrderAsync(latestOrderNumber.Value);
    }

    private static async Task AddOrIncrementLineAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        IDbTransaction transaction,
        long orderNumber,
        string sku,
        int quantity,
        string now)
    {
        const string existingSql = """
            SELECT id, quantity
            FROM orders
            WHERE order_number = @OrderNumber
              AND lower(sku) = lower(@Sku)
            LIMIT 1;
            """;

        var existing = await connection.QuerySingleOrDefaultAsync<(long Id, int Quantity)>(
            existingSql,
            new
            {
                OrderNumber = orderNumber,
                Sku = sku
            },
            transaction);

        if (existing.Id > 0)
        {
            const string updateSql = """
                UPDATE orders
                SET quantity = quantity + @Quantity,
                    updated_utc = @UpdatedUtc
                WHERE id = @Id;
                """;

            await connection.ExecuteAsync(
                updateSql,
                new
                {
                    existing.Id,
                    Quantity = quantity,
                    UpdatedUtc = now
                },
                transaction);

            return;
        }

        const string insertSql = """
            INSERT INTO orders (order_number, sku, quantity, created_utc, updated_utc)
            VALUES (@OrderNumber, @Sku, @Quantity, @CreatedUtc, @UpdatedUtc);
            """;

        await connection.ExecuteAsync(
            insertSql,
            new
            {
                OrderNumber = orderNumber,
                Sku = sku,
                Quantity = quantity,
                CreatedUtc = now,
                UpdatedUtc = now
            },
            transaction);
    }

    private static IReadOnlyList<OrderItemRequest> NormalizeItems(IReadOnlyList<OrderItemRequest> items)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("At least one order item is required.", nameof(items));
        }

        var normalized = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Sku))
            .Select(item => new OrderItemRequest
            {
                Sku = item.Sku.Trim(),
                Quantity = item.Quantity
            })
            .Where(item => item.Quantity > 0)
            .GroupBy(item => item.Sku, StringComparer.OrdinalIgnoreCase)
            .Select(group => new OrderItemRequest
            {
                Sku = group.First().Sku,
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToList();

        if (normalized.Count == 0)
        {
            throw new ArgumentException("At least one valid order item with quantity greater than zero is required.", nameof(items));
        }

        return normalized;
    }

    private static OrderDetails BuildOrderDetails(long orderNumber, List<OrderLine> lines)
    {
        var createdUtc = lines
            .Select(line => line.CreatedUtc)
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault() ?? string.Empty;

        var updatedUtc = lines
            .Select(line => line.UpdatedUtc)
            .OrderByDescending(value => value, StringComparer.Ordinal)
            .FirstOrDefault() ?? createdUtc;

        return new OrderDetails
        {
            OrderNumber = orderNumber,
            CreatedUtc = createdUtc,
            UpdatedUtc = updatedUtc,
            LineCount = lines.Count,
            TotalQuantity = lines.Sum(line => line.Quantity),
            Items = lines
        };
    }
}
