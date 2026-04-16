using System.ComponentModel;
using InventoryDemo.Server.DTOs;
using InventoryDemo.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace InventoryDemo.Server.Services;

[McpServerToolType]
public sealed class InventoryMcpTools
{
    public sealed class OrderToolItem
    {
        [Description("Inventory item identifier, either SKU or item name.")]
        public string ItemNameOrSku { get; set; } = string.Empty;

        [Description("Quantity to order. Must be greater than zero.")]
        public int Quantity { get; set; }
    }

    [McpServerTool(
        Name = "inventory_list_items",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false)]
    [Description("List current inventory items with SKU, name, and quantity.")]
    public static async Task<object> ListInventoryItemsAsync(
        IServiceProvider serviceProvider,
        [Description("Maximum number of items to return (1 to 200).")] int limit = 50)
    {
        var inventoryService = serviceProvider.GetRequiredService<InventoryService>();

        var safeLimit = Math.Clamp(limit, 1, 200);
        var items = await inventoryService.GetItemsAsync();
        var selected = items.Take(safeLimit).ToList();

        return new
        {
            total = items.Count,
            returned = selected.Count,
            items = selected.Select(item => new
            {
                id = item.Id,
                sku = item.Sku,
                name = item.Name,
                quantity = item.Quantity,
                updatedUtc = item.UpdatedUtc
            })
        };
    }

    [McpServerTool(
        Name = "inventory_search_status",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false)]
    [Description("Search inventory item status by name or SKU to answer questions like 'do I have X?' or 'how many X do I have?'.")]
    public static async Task<object> SearchInventoryStatusAsync(
        [Description("Item name or SKU fragment to search for.")] string query,
        IServiceProvider serviceProvider,
        [Description("Maximum number of matching items to return (1 to 50).")] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new
            {
                found = false,
                message = "Provide a non-empty search query."
            };
        }

        var inventoryService = serviceProvider.GetRequiredService<InventoryService>();

        var matches = await inventoryService.SearchItemsByNameOrSkuAsync(query, Math.Clamp(limit, 1, 50));

        return new
        {
            found = matches.Count > 0,
            query = query.Trim(),
            count = matches.Count,
            items = matches.Select(item => new
            {
                id = item.Id,
                sku = item.Sku,
                name = item.Name,
                quantity = item.Quantity,
                inStock = item.Quantity > 0
            })
        };
    }

    [McpServerTool(
        Name = "inventory_add_transaction",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false)]
    [Description("Add an inventory transaction by item name or SKU and adjust on-hand quantity.")]
    public static async Task<object> AddInventoryTransactionAsync(
        [Description("Item name or SKU to identify which inventory item should be adjusted.")] string itemNameOrSku,
        [Description("Quantity change to apply. Positive adds stock, negative removes stock.")] int quantityDelta,
        IServiceProvider serviceProvider,
        [Description("Transaction type label such as adjustment, restock, sale, or audit.")] string transactionType = "adjustment",
        [Description("Optional note describing the reason for this change.")] string? note = null)
    {
        if (string.IsNullOrWhiteSpace(itemNameOrSku))
        {
            return new
            {
                success = false,
                message = "itemNameOrSku is required."
            };
        }

        var inventoryService = serviceProvider.GetRequiredService<InventoryService>();

        var normalizedLookup = itemNameOrSku.Trim();
        var matches = await inventoryService.SearchItemsByNameOrSkuAsync(normalizedLookup, 10);
        if (matches.Count == 0)
        {
            return new
            {
                success = false,
                message = $"No inventory item found matching '{normalizedLookup}'."
            };
        }

        var exactMatches = matches
            .Where(item => InventoryService.AreEquivalentForSearch(item.Name, normalizedLookup)
                           || InventoryService.AreEquivalentForSearch(item.Sku, normalizedLookup))
            .ToList();

        var selectedItem = exactMatches.Count switch
        {
            1 => exactMatches[0],
            0 when matches.Count == 1 => matches[0],
            _ => null
        };

        if (selectedItem is null)
        {
            return new
            {
                success = false,
                message = $"Multiple items match '{normalizedLookup}'. Use a more specific name or SKU.",
                candidates = matches.Select(item => new
                {
                    id = item.Id,
                    sku = item.Sku,
                    name = item.Name
                })
            };
        }

        var appliedTransaction = await inventoryService.AddInventoryTransactionAsync(
            selectedItem.Id,
            transactionType,
            quantityDelta,
            note);

        if (appliedTransaction is null)
        {
            return new
            {
                success = false,
                message = "Could not apply inventory transaction."
            };
        }

        var refreshedItem = await inventoryService.GetItemAsync(selectedItem.Id);

        return new
        {
            success = true,
            transaction = new
            {
                id = appliedTransaction.Id,
                itemId = appliedTransaction.ItemId,
                itemSku = appliedTransaction.ItemSku,
                itemName = appliedTransaction.ItemName,
                transactionType = appliedTransaction.TransactionType,
                quantityDelta = appliedTransaction.QuantityDelta,
                note = appliedTransaction.Note,
                createdUtc = appliedTransaction.CreatedUtc
            },
            item = refreshedItem is null
                ? null
                : new
                {
                    id = refreshedItem.Id,
                    sku = refreshedItem.Sku,
                    name = refreshedItem.Name,
                    quantity = refreshedItem.Quantity
                }
        };
    }

    [McpServerTool(
        Name = "orders_get_latest",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false)]
    [Description("Get the latest order and its item lines.")]
    public static async Task<object> GetLatestOrderAsync(IServiceProvider serviceProvider)
    {
        var orderService = serviceProvider.GetRequiredService<OrderService>();
        var order = await orderService.GetLatestOrderAsync();
        if (order is null)
        {
            return new
            {
                found = false,
                message = "No orders found yet."
            };
        }

        return new
        {
            found = true,
            order
        };
    }

    [McpServerTool(
        Name = "orders_create",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false)]
    [Description("Create a new order with one or more item lines.")]
    public static async Task<object> CreateOrderAsync(
        [Description("Array of order lines. Each line requires itemNameOrSku and quantity > 0.")]
        List<OrderToolItem> items,
        IServiceProvider serviceProvider)
    {
        if (items.Count == 0)
        {
            return new
            {
                success = false,
                message = "At least one item is required to create an order."
            };
        }

        var inventoryService = serviceProvider.GetRequiredService<InventoryService>();
        var orderService = serviceProvider.GetRequiredService<OrderService>();

        var resolvedItems = new List<OrderItemRequest>();
        var unresolved = new List<object>();
        foreach (var item in items)
        {
            if (item.Quantity <= 0)
            {
                unresolved.Add(new
                {
                    itemNameOrSku = item.ItemNameOrSku,
                    message = "Quantity must be greater than zero."
                });
                continue;
            }

            var resolution = await ResolveItemByNameOrSkuAsync(item.ItemNameOrSku, inventoryService);
            if (!resolution.Success)
            {
                unresolved.Add(new
                {
                    itemNameOrSku = item.ItemNameOrSku,
                    message = resolution.Message,
                    candidates = resolution.Candidates
                });
                continue;
            }

            resolvedItems.Add(new OrderItemRequest
            {
                Sku = resolution.Item!.Sku,
                Quantity = item.Quantity
            });
        }

        if (unresolved.Count > 0)
        {
            return new
            {
                success = false,
                message = "Some order lines could not be resolved. No order was created.",
                unresolved
            };
        }

        var order = await orderService.CreateOrderAsync(resolvedItems);
        return new
        {
            success = true,
            message = $"Created order {order.OrderNumber}.",
            order
        };
    }

    [McpServerTool(
        Name = "orders_add_items_to_latest",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false)]
    [Description("Add one or more item lines to the latest existing order.")]
    public static async Task<object> AddItemsToLatestOrderAsync(
        [Description("Array of order lines. Each line requires itemNameOrSku and quantity > 0.")]
        List<OrderToolItem> items,
        IServiceProvider serviceProvider)
    {
        if (items.Count == 0)
        {
            return new
            {
                success = false,
                message = "At least one item is required to update the latest order."
            };
        }

        var inventoryService = serviceProvider.GetRequiredService<InventoryService>();
        var orderService = serviceProvider.GetRequiredService<OrderService>();

        var resolvedItems = new List<OrderItemRequest>();
        var unresolved = new List<object>();
        foreach (var item in items)
        {
            if (item.Quantity <= 0)
            {
                unresolved.Add(new
                {
                    itemNameOrSku = item.ItemNameOrSku,
                    message = "Quantity must be greater than zero."
                });
                continue;
            }

            var resolution = await ResolveItemByNameOrSkuAsync(item.ItemNameOrSku, inventoryService);
            if (!resolution.Success)
            {
                unresolved.Add(new
                {
                    itemNameOrSku = item.ItemNameOrSku,
                    message = resolution.Message,
                    candidates = resolution.Candidates
                });
                continue;
            }

            resolvedItems.Add(new OrderItemRequest
            {
                Sku = resolution.Item!.Sku,
                Quantity = item.Quantity
            });
        }

        if (unresolved.Count > 0)
        {
            return new
            {
                success = false,
                message = "Some order lines could not be resolved. Latest order was not updated.",
                unresolved
            };
        }

        var order = await orderService.AddItemsToLatestOrderAsync(resolvedItems);
        if (order is null)
        {
            return new
            {
                success = false,
                message = "No existing order found. Create an order first."
            };
        }

        return new
        {
            success = true,
            message = $"Updated latest order {order.OrderNumber}.",
            order
        };
    }

    private static async Task<ItemResolutionResult> ResolveItemByNameOrSkuAsync(
        string itemNameOrSku,
        InventoryService inventoryService)
    {
        if (string.IsNullOrWhiteSpace(itemNameOrSku))
        {
            return ItemResolutionResult.Failure("itemNameOrSku is required.");
        }

        var normalizedLookup = itemNameOrSku.Trim();
        var matches = await inventoryService.SearchItemsByNameOrSkuAsync(normalizedLookup, 10);
        if (matches.Count == 0)
        {
            return ItemResolutionResult.Failure($"No inventory item found matching '{normalizedLookup}'.");
        }

        var exactMatches = matches
            .Where(item => InventoryService.AreEquivalentForSearch(item.Name, normalizedLookup)
                           || InventoryService.AreEquivalentForSearch(item.Sku, normalizedLookup))
            .ToList();

        var selectedItem = exactMatches.Count switch
        {
            1 => exactMatches[0],
            0 when matches.Count == 1 => matches[0],
            _ => null
        };

        if (selectedItem is null)
        {
            return ItemResolutionResult.Failure(
                $"Multiple items match '{normalizedLookup}'. Use a more specific name or SKU.",
                matches.Select(item => new
                {
                    id = item.Id,
                    sku = item.Sku,
                    name = item.Name
                }));
        }

        return ItemResolutionResult.FromSuccess(selectedItem);
    }

    private sealed class ItemResolutionResult
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public InventoryItem? Item { get; init; }

        public object? Candidates { get; init; }

        public static ItemResolutionResult FromSuccess(InventoryItem item)
        {
            return new ItemResolutionResult
            {
                Success = true,
                Item = item
            };
        }

        public static ItemResolutionResult Failure(string message, object? candidates = null)
        {
            return new ItemResolutionResult
            {
                Success = false,
                Message = message,
                Candidates = candidates
            };
        }
    }
}
