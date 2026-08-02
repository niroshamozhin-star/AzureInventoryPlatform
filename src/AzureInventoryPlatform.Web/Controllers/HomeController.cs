using System.Diagnostics;
using AzureInventoryPlatform.Web.Models;
using AzureInventoryPlatform.Web.Repositories;
using AzureInventoryPlatform.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Web.Controllers;

public class HomeController : Controller
{
    private readonly IRepository<Product> _products;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IRepository<InventoryItem> _inventory;

    public HomeController(
        IRepository<Product> products,
        IRepository<Warehouse> warehouses,
        IRepository<InventoryItem> inventory)
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
        var lowStockCount = inventory.Count(i => i.QuantityOnHand <= i.ReorderLevel);

        var viewModel = new DashboardViewModel(products.Count, warehouses.Count, inventory.Count, lowStockCount);
        return View(viewModel);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
