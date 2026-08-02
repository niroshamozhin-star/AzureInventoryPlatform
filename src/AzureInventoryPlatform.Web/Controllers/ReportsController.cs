using AzureInventoryPlatform.Web.Models;
using AzureInventoryPlatform.Web.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Web.Controllers;

public class ReportsController : Controller
{
    private readonly IRepository<InventoryItem> _inventory;
    private readonly IRepository<Product> _products;
    private readonly IRepository<Warehouse> _warehouses;

    public ReportsController(
        IRepository<InventoryItem> inventory,
        IRepository<Product> products,
        IRepository<Warehouse> warehouses)
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
