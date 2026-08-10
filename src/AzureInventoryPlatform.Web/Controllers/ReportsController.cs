using AzureInventoryPlatform.Web.Data;
using AzureInventoryPlatform.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Web.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly InventoryData _inventory;
    private readonly ProductData _products;
    private readonly WarehouseData _warehouses;
    private readonly LowStockSnapshotStore _snapshots;

    public ReportsController(InventoryData inventory, ProductData products, WarehouseData warehouses, LowStockSnapshotStore snapshots)
    {
        _inventory = inventory;
        _products = products;
        _warehouses = warehouses;
        _snapshots = snapshots;
    }

    public IActionResult Index() => RedirectToAction(nameof(InventoryByWarehouse));

    public async Task<IActionResult> InventoryByWarehouse()
    {
        var (_, products, warehouses) = await LoadDataAsync();

        var stockValue = (await _inventory.GetAllAsync())
            .Where(i => products.ContainsKey(i.ProductId) && warehouses.ContainsKey(i.WarehouseId))
            .GroupBy(i => i.WarehouseId)
            .Select(g => new WarehouseStockSummary(
                g.Key,
                warehouses[g.Key].WarehouseName,
                g.Sum(i => i.Quantity),
                g.Sum(i => i.Quantity * products[i.ProductId].UnitPrice)))
            .OrderBy(s => s.WarehouseName)
            .ToList();

        return View(stockValue);
    }

    public async Task<IActionResult> LowStock()
    {
        var (inventory, products, warehouses) = await LoadDataAsync();

        var lowStock = inventory
            .Where(i => products.ContainsKey(i.ProductId) && warehouses.ContainsKey(i.WarehouseId))
            .Where(i => i.Quantity <= products[i.ProductId].ReorderLevel)
            .Select(i => new LowStockAlert(
                i.Id,
                i.ProductId,
                products[i.ProductId].ProductName,
                i.WarehouseId,
                warehouses[i.WarehouseId].WarehouseName,
                i.Quantity,
                products[i.ProductId].ReorderLevel))
            .OrderBy(a => a.Quantity)
            .ToList();

        // Phase 7: shows the cached snapshot the Functions app's timer trigger
        // last wrote to Cosmos DB, alongside the live SQL numbers above - not
        // a replacement for them, just a visible proof the cache is real.
        ViewBag.CosmosSnapshot = await _snapshots.GetLatestAsync();

        return View(lowStock);
    }

    private async Task<(IReadOnlyList<InventoryItem> Inventory, Dictionary<int, Product> Products, Dictionary<int, Warehouse> Warehouses)> LoadDataAsync()
    {
        var inventory = await _inventory.GetAllAsync();
        var products = (await _products.GetAllAsync()).ToDictionary(p => p.Id);
        var warehouses = (await _warehouses.GetAllAsync()).ToDictionary(w => w.Id);
        return (inventory, products, warehouses);
    }
}
