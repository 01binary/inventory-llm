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
        [Description("Product display name or partial display name to search for, such as 'clamato'. Use an explicit SKU only if the user says it is a SKU.")]
        public string ProductName { get; set; } = string.Empty;

        [Description("Quantity for this order line. Must be greater than zero.")]
        public int Quantity { get; set; }
    }

    public sealed class ProductLookupItem
    {
        [Description("Product display name or partial display name to search for, such as 'clamato'. Use an explicit SKU only if the user says it is a SKU.")]
        public string ProductName { get; set; } = string.Empty;
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
    [Description("Search inventory by product display name or explicit SKU. Partial names such as one word are valid. If one product matches, use its SKU without asking the user to confirm.")]
    public static async Task<object> SearchInventoryStatusAsync(
        [Description("Product display name, partial display name, or explicit SKU fragment to search for.")] string query,
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
    [Description("Add an inventory transaction by product display name or explicit SKU and adjust on-hand quantity. Partial product names are valid; if one product matches, use it without asking for SKU confirmation.")]
    public static async Task<object> AddInventoryTransactionAsync(
        [Description("Product display name or partial display name to search for. Use an explicit SKU only if the user says it is a SKU.")] string productName,
        [Description("Quantity change to apply. Positive adds stock, negative removes stock.")] int quantityDelta,
        IServiceProvider serviceProvider,
        [Description("Transaction type label such as adjustment, restock, sale, or audit.")] string transactionType = "adjustment",
        [Description("Optional note describing the reason for this change.")] string? note = null)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return new
            {
                success = false,
                message = "productName is required."
            };
        }

        var inventoryService = serviceProvider.GetRequiredService<InventoryService>();

        var normalizedLookup = productName.Trim();
        var matches = await inventoryService.SearchItemsByNameOrSkuAsync(normalizedLookup, 10);
        if (matches.Count == 0)
        {
            return new
            {
                success = false,
                message = $"No product found matching '{normalizedLookup}'."
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
                message = $"Multiple products match '{normalizedLookup}'. Ask the user to choose the intended product by display name.",
                candidates = matches.Select(item => new
                {
                    id = item.Id,
                    sku = item.Sku,
                    name = item.Name,
                    quantity = item.Quantity
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
                },
            resolvedProduct = new
            {
                requestedName = normalizedLookup,
                sku = selectedItem.Sku,
                name = selectedItem.Name,
                quantityDelta
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
    [Description("Create a new order with one or more product lines. Product names can be partial display names; the tool resolves SKUs internally and does not require user SKU confirmation when exactly one product matches.")]
    public static async Task<object> CreateOrderAsync(
        [Description("Array of order lines. Each line requires productName and quantity > 0.")]
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
        var resolvedProducts = new List<object>();
        var unresolved = new List<object>();
        foreach (var item in items)
        {
            if (item.Quantity <= 0)
            {
                unresolved.Add(new
                {
                    productName = item.ProductName,
                    message = "Quantity must be greater than zero."
                });
                continue;
            }

            var resolution = await ResolveProductAsync(item.ProductName, inventoryService);
            if (!resolution.Success)
            {
                unresolved.Add(new
                {
                    productName = item.ProductName,
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
            resolvedProducts.Add(new
            {
                requestedName = item.ProductName.Trim(),
                sku = resolution.Item.Sku,
                name = resolution.Item.Name,
                quantity = item.Quantity
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
            resolvedProducts,
            order
        };
    }

    [McpServerTool(
        Name = "orders_add_items_to_latest",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false)]
    [Description("Add one or more product lines to the latest existing order. Product names can be partial display names; the tool resolves SKUs internally and does not require user SKU confirmation when exactly one product matches.")]
    public static async Task<object> AddItemsToLatestOrderAsync(
        [Description("Array of order lines. Each line requires productName and quantity > 0.")]
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
        var resolvedProducts = new List<object>();
        var unresolved = new List<object>();
        foreach (var item in items)
        {
            if (item.Quantity <= 0)
            {
                unresolved.Add(new
                {
                    productName = item.ProductName,
                    message = "Quantity must be greater than zero."
                });
                continue;
            }

            var resolution = await ResolveProductAsync(item.ProductName, inventoryService);
            if (!resolution.Success)
            {
                unresolved.Add(new
                {
                    productName = item.ProductName,
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
            resolvedProducts.Add(new
            {
                requestedName = item.ProductName.Trim(),
                sku = resolution.Item.Sku,
                name = resolution.Item.Name,
                quantity = item.Quantity
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
            resolvedProducts,
            order
        };
    }

    [McpServerTool(
        Name = "orders_set_latest_item_quantities",
        ReadOnly = false,
        Idempotent = true,
        Destructive = false)]
    [Description("Set existing product line quantities on the latest order. Use this when the user asks to change, edit, fix, or set the quantity of an item already on the latest order. Product names can be partial display names; the tool resolves SKUs internally and does not require user SKU confirmation when exactly one product matches.")]
    public static async Task<object> SetLatestOrderItemQuantitiesAsync(
        [Description("Array of order line quantity edits. Each line requires productName and the new final quantity > 0.")]
        List<OrderToolItem> items,
        IServiceProvider serviceProvider)
    {
        if (items.Count == 0)
        {
            return new
            {
                success = false,
                message = "At least one item is required to edit the latest order."
            };
        }

        var inventoryService = serviceProvider.GetRequiredService<InventoryService>();
        var orderService = serviceProvider.GetRequiredService<OrderService>();

        var latestOrder = await orderService.GetLatestOrderAsync();
        if (latestOrder is null)
        {
            return new
            {
                success = false,
                message = "No existing order found. Create an order first."
            };
        }

        var resolvedItems = new List<OrderItemRequest>();
        var resolvedProducts = new List<object>();
        var unresolved = new List<object>();
        var missingFromOrder = new List<object>();

        foreach (var item in items)
        {
            if (item.Quantity <= 0)
            {
                unresolved.Add(new
                {
                    productName = item.ProductName,
                    message = "Quantity must be greater than zero. Use the remove tool to delete an item from the order."
                });
                continue;
            }

            var resolution = await ResolveProductAsync(item.ProductName, inventoryService);
            if (!resolution.Success)
            {
                unresolved.Add(new
                {
                    productName = item.ProductName,
                    message = resolution.Message,
                    candidates = resolution.Candidates
                });
                continue;
            }

            var existingLine = latestOrder.Items.FirstOrDefault(line =>
                string.Equals(line.Sku, resolution.Item!.Sku, StringComparison.OrdinalIgnoreCase));
            if (existingLine is null)
            {
                missingFromOrder.Add(new
                {
                    requestedName = item.ProductName.Trim(),
                    sku = resolution.Item!.Sku,
                    name = resolution.Item.Name,
                    message = "Product is not on the latest order."
                });
                continue;
            }

            resolvedItems.Add(new OrderItemRequest
            {
                Sku = resolution.Item!.Sku,
                Quantity = item.Quantity
            });
            resolvedProducts.Add(new
            {
                requestedName = item.ProductName.Trim(),
                sku = resolution.Item.Sku,
                name = resolution.Item.Name,
                previousQuantity = existingLine.Quantity,
                quantity = item.Quantity
            });
        }

        if (unresolved.Count > 0 || missingFromOrder.Count > 0)
        {
            return new
            {
                success = false,
                message = "Some quantity edits could not be applied. Latest order was not updated.",
                unresolved,
                missingFromOrder
            };
        }

        var order = await orderService.SetLatestOrderItemQuantitiesAsync(resolvedItems);
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
            message = $"Updated quantities on latest order {order.OrderNumber}.",
            resolvedProducts,
            order
        };
    }

    [McpServerTool(
        Name = "orders_remove_items_from_latest",
        ReadOnly = false,
        Idempotent = true,
        Destructive = false)]
    [Description("Remove one or more existing product lines from the latest order. Use this when the user asks to remove, delete, drop, or take an item off the latest order. Product names can be partial display names; the tool resolves SKUs internally and does not require user SKU confirmation when exactly one product matches.")]
    public static async Task<object> RemoveItemsFromLatestOrderAsync(
        [Description("Array of products to remove. Each entry requires productName.")]
        List<ProductLookupItem> items,
        IServiceProvider serviceProvider)
    {
        if (items.Count == 0)
        {
            return new
            {
                success = false,
                message = "At least one item is required to remove from the latest order."
            };
        }

        var inventoryService = serviceProvider.GetRequiredService<InventoryService>();
        var orderService = serviceProvider.GetRequiredService<OrderService>();

        var latestOrder = await orderService.GetLatestOrderAsync();
        if (latestOrder is null)
        {
            return new
            {
                success = false,
                message = "No existing order found. Create an order first."
            };
        }

        var resolvedSkus = new List<string>();
        var removedProducts = new List<object>();
        var unresolved = new List<object>();
        var missingFromOrder = new List<object>();

        foreach (var item in items)
        {
            var resolution = await ResolveProductAsync(item.ProductName, inventoryService);
            if (!resolution.Success)
            {
                unresolved.Add(new
                {
                    productName = item.ProductName,
                    message = resolution.Message,
                    candidates = resolution.Candidates
                });
                continue;
            }

            var existingLine = latestOrder.Items.FirstOrDefault(line =>
                string.Equals(line.Sku, resolution.Item!.Sku, StringComparison.OrdinalIgnoreCase));
            if (existingLine is null)
            {
                missingFromOrder.Add(new
                {
                    requestedName = item.ProductName.Trim(),
                    sku = resolution.Item!.Sku,
                    name = resolution.Item.Name,
                    message = "Product is not on the latest order."
                });
                continue;
            }

            resolvedSkus.Add(resolution.Item!.Sku);
            removedProducts.Add(new
            {
                requestedName = item.ProductName.Trim(),
                sku = resolution.Item.Sku,
                name = resolution.Item.Name,
                removedQuantity = existingLine.Quantity
            });
        }

        if (unresolved.Count > 0 || missingFromOrder.Count > 0)
        {
            return new
            {
                success = false,
                message = "Some items could not be removed. Latest order was not updated.",
                unresolved,
                missingFromOrder
            };
        }

        var order = await orderService.RemoveItemsFromLatestOrderAsync(resolvedSkus);

        return new
        {
            success = true,
            message = order is null
                ? "Removed items from the latest order. The order now has no remaining items."
                : $"Removed items from latest order {order.OrderNumber}.",
            removedProducts,
            order
        };
    }

    private static async Task<ItemResolutionResult> ResolveProductAsync(
        string productName,
        InventoryService inventoryService)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return ItemResolutionResult.Failure("productName is required.");
        }

        var normalizedLookup = productName.Trim();
        var matches = await inventoryService.SearchItemsByNameOrSkuAsync(normalizedLookup, 10);
        if (matches.Count == 0)
        {
            return ItemResolutionResult.Failure($"No product found matching '{normalizedLookup}'.");
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
                $"Multiple products match '{normalizedLookup}'. Ask the user to choose the intended product by display name.",
                matches.Select(item => new
                {
                    id = item.Id,
                    sku = item.Sku,
                    name = item.Name,
                    quantity = item.Quantity
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
