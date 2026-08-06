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

        // Products can now be cleared independently of Warehouses (e.g. a manual
        // reset), so "Products is empty" alone no longer proves this is a fresh
        // database - also check the seed warehouses specifically, so a cleared
        // Products table doesn't cause a second, colliding insert of SEED-E/SEED-W.
        var existingWarehouses = await warehouses.GetAllAsync();
        if ((await products.GetAllAsync()).Count > 0 ||
            existingWarehouses.Any(w => w.WarehouseCode is "SEED-E" or "SEED-W"))
        {
            return;
        }

        var widget = await products.AddAsync(new Product { ProductCode = "WID-001", ProductName = "Widget", UnitPrice = 12.50m, ReorderLevel = 100 });
        var gadget = await products.AddAsync(new Product { ProductCode = "GAD-002", ProductName = "Gadget", UnitPrice = 45.00m, ReorderLevel = 25 });

        // "SEED-" prefix keeps these demo codes from ever colliding with the
        // WH-N/WH-S/WH-E/WH-W/WH-C codes used by the sample import spreadsheets.
        var east = await warehouses.AddAsync(new Warehouse { WarehouseCode = "SEED-E", WarehouseName = "East DC", City = "Columbus" });
        var west = await warehouses.AddAsync(new Warehouse { WarehouseCode = "SEED-W", WarehouseName = "West DC", City = "Reno" });

        await inventory.AddAsync(new InventoryItem { ProductId = widget.Id, WarehouseId = east.Id, Quantity = 500 });
        await inventory.AddAsync(new InventoryItem { ProductId = gadget.Id, WarehouseId = east.Id, Quantity = 20 });
        await inventory.AddAsync(new InventoryItem { ProductId = gadget.Id, WarehouseId = west.Id, Quantity = 150 });
    }
}
