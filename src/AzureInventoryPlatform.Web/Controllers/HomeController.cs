using System.Diagnostics;
using AzureInventoryPlatform.Web.Data;
using AzureInventoryPlatform.Web.Models;
using AzureInventoryPlatform.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ProductData _products;
    private readonly WarehouseData _warehouses;
    private readonly InventoryData _inventory;

    public HomeController(ProductData products, WarehouseData warehouses, InventoryData inventory)
    {
        _products = products;
        _warehouses = warehouses;
        _inventory = inventory;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _products.GetAllAsync();
        var warehouses = await _warehouses.GetAllAsync();
        var inventory = await _inventory.GetAllAsync();
        var productsById = products.ToDictionary(p => p.Id);
        var lowStockCount = inventory.Count(i =>
            productsById.TryGetValue(i.ProductId, out var product) && i.Quantity <= product.ReorderLevel);

        var viewModel = new DashboardViewModel(products.Count, warehouses.Count, inventory.Count, lowStockCount);
        return View(viewModel);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
