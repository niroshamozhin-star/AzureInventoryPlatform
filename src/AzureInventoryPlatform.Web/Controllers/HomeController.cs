using System.Diagnostics;
using AzureInventoryPlatform.Web.ApiClients;
using AzureInventoryPlatform.Web.Models;
using AzureInventoryPlatform.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Web.Controllers;

public class HomeController : Controller
{
    private readonly IProductApiClient _products;
    private readonly IWarehouseApiClient _warehouses;
    private readonly IInventoryApiClient _inventory;
    private readonly IReportApiClient _reports;

    public HomeController(
        IProductApiClient products,
        IWarehouseApiClient warehouses,
        IInventoryApiClient inventory,
        IReportApiClient reports)
    {
        _products = products;
        _warehouses = warehouses;
        _inventory = inventory;
        _reports = reports;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _products.GetAllAsync();
        var warehouses = await _warehouses.GetAllAsync();
        var inventory = await _inventory.GetAllAsync();
        var lowStock = await _reports.GetLowStockAsync();

        var viewModel = new DashboardViewModel(products.Count, warehouses.Count, inventory.Count, lowStock.Count);
        return View(viewModel);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
