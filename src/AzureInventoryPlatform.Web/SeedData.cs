using AzureInventoryPlatform.Web.Data;
using AzureInventoryPlatform.Web.Models;

namespace AzureInventoryPlatform.Web;

public static class SeedData
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<ProductData>();
        var warehouses = scope.ServiceProvider.GetRequiredService<WarehouseData>();
        var inventory = scope.ServiceProvider.GetRequiredService<InventoryData>();

        if ((await products.GetAllAsync()).Count > 0)
        {
            return;
        }

        var widget = await products.AddAsync(new Product { Sku = "WID-001", Name = "Widget", UnitPrice = 12.50m, Category = "Hardware" });
        var gadget = await products.AddAsync(new Product { Sku = "GAD-002", Name = "Gadget", UnitPrice = 45.00m, Category = "Electronics" });

        var east = await warehouses.AddAsync(new Warehouse { Name = "East DC", Location = "Columbus, OH", Capacity = 10000 });
        var west = await warehouses.AddAsync(new Warehouse { Name = "West DC", Location = "Reno, NV", Capacity = 8000 });

        await inventory.AddAsync(new InventoryItem { ProductId = widget.Id, WarehouseId = east.Id, QuantityOnHand = 500, ReorderLevel = 100 });
        await inventory.AddAsync(new InventoryItem { ProductId = gadget.Id, WarehouseId = east.Id, QuantityOnHand = 20, ReorderLevel = 25 });
        await inventory.AddAsync(new InventoryItem { ProductId = gadget.Id, WarehouseId = west.Id, QuantityOnHand = 150, ReorderLevel = 30 });
    }
}
