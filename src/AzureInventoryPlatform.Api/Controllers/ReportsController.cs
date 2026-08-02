using AzureInventoryPlatform.Contracts.Models;
using AzureInventoryPlatform.Api.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
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

    [HttpGet("stock-summary")]
    public async Task<ActionResult<IReadOnlyList<WarehouseStockSummary>>> StockSummary()
    {
        var inventory = await _inventory.GetAllAsync();
        var products = (await _products.GetAllAsync()).ToDictionary(p => p.Id);
        var warehouses = (await _warehouses.GetAllAsync()).ToDictionary(w => w.Id);

        var summary = inventory
            .Where(i => warehouses.ContainsKey(i.WarehouseId) && products.ContainsKey(i.ProductId))
            .GroupBy(i => i.WarehouseId)
            .Select(g => new WarehouseStockSummary(
                g.Key,
                warehouses[g.Key].Name,
                g.Sum(i => i.QuantityOnHand),
                g.Sum(i => i.QuantityOnHand * products[i.ProductId].UnitPrice)))
            .OrderBy(s => s.WarehouseName)
            .ToList();

        return Ok(summary);
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<IReadOnlyList<LowStockAlert>>> LowStock()
    {
        var inventory = await _inventory.GetAllAsync();
        var products = (await _products.GetAllAsync()).ToDictionary(p => p.Id);
        var warehouses = (await _warehouses.GetAllAsync()).ToDictionary(w => w.Id);

        var alerts = inventory
            .Where(i => i.QuantityOnHand <= i.ReorderLevel)
            .Where(i => products.ContainsKey(i.ProductId) && warehouses.ContainsKey(i.WarehouseId))
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

        return Ok(alerts);
    }
}
