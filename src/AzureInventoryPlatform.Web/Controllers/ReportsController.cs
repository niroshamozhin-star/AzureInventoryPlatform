using AzureInventoryPlatform.Web.Data;
using AzureInventoryPlatform.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Web.Controllers;

public class ReportsController : Controller
{
    private readonly InventoryData _inventory;
    private readonly ProductData _products;
    private readonly WarehouseData _warehouses;

    public ReportsController(InventoryData inventory, ProductData products, WarehouseData warehouses)
    {
        _inventory = inventory;
        _products = products;
        _warehouses = warehouses;
    }

    public async Task<IActionResult> Index()
    {
        var inventory = await _inventory.GetAllAsync();
        var products = (await _products.GetAllAsync()).ToDictionary(p => p.Id);
        var warehouses = (await _warehouses.GetAllAsync()).ToDictionary(w => w.Id);

        var relevantInventory = inventory
            .Where(i => products.ContainsKey(i.ProductId) && warehouses.ContainsKey(i.WarehouseId))
            .ToList();

        var stockSummary = relevantInventory
            .GroupBy(i => i.WarehouseId)
            .Select(g => new WarehouseStockSummary(
                g.Key,
                warehouses[g.Key].Name,
                g.Sum(i => i.QuantityOnHand),
                g.Sum(i => i.QuantityOnHand * products[i.ProductId].UnitPrice)))
            .OrderBy(s => s.WarehouseName)
            .ToList();

        var lowStock = relevantInventory
            .Where(i => i.QuantityOnHand <= i.ReorderLevel)
            .Select(i => new LowStockAlert(
                i.Id,
                i.ProductId,
                products[i.ProductId].Name,
                i.WarehouseId,
                warehouses[i.WarehouseId].Name,
                i.QuantityOnHand,
                i.ReorderLevel))
            .OrderBy(a => a.QuantityOnHand)
            .ToList();

        ViewBag.LowStock = lowStock;
        return View(stockSummary);
    }
}
