using AzureInventoryPlatform.Contracts.Models;
using AzureInventoryPlatform.Web.ApiClients;
using AzureInventoryPlatform.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AzureInventoryPlatform.Web.Controllers;

public class InventoryController : Controller
{
    private readonly IInventoryApiClient _inventory;
    private readonly IProductApiClient _products;
    private readonly IWarehouseApiClient _warehouses;

    public InventoryController(
        IInventoryApiClient inventory,
        IProductApiClient products,
        IWarehouseApiClient warehouses)
    {
        _inventory = inventory;
        _products = products;
        _warehouses = warehouses;
    }

    public async Task<IActionResult> Index()
    {
        var items = await _inventory.GetAllAsync();
        var products = (await _products.GetAllAsync()).ToDictionary(p => p.Id);
        var warehouses = (await _warehouses.GetAllAsync()).ToDictionary(w => w.Id);

        var viewModel = items
            .Select(i => new InventoryListItem(
                i.Id,
                i.ProductId,
                products.TryGetValue(i.ProductId, out var product) ? product.Name : "(unknown product)",
                i.WarehouseId,
                warehouses.TryGetValue(i.WarehouseId, out var warehouse) ? warehouse.Name : "(unknown warehouse)",
                i.QuantityOnHand,
                i.ReorderLevel))
            .OrderBy(i => i.WarehouseName)
            .ThenBy(i => i.ProductName)
            .ToList();

        return View(viewModel);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        return View(new InventoryItem());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InventoryItem item)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(item);
        }

        var (success, error) = await _inventory.CreateAsync(item);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "Could not create inventory item.");
            await PopulateDropdownsAsync();
            return View(item);
        }

        TempData["Success"] = "Inventory item created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Adjust(int id)
    {
        var item = await _inventory.GetByIdAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        var product = await _products.GetByIdAsync(item.ProductId);
        var warehouse = await _warehouses.GetByIdAsync(item.WarehouseId);
        ViewBag.ProductName = product?.Name ?? "(unknown product)";
        ViewBag.WarehouseName = warehouse?.Name ?? "(unknown warehouse)";
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(int id, int delta)
    {
        var (success, error) = await _inventory.AdjustAsync(id, delta);
        if (!success)
        {
            TempData["Error"] = error ?? "Could not adjust quantity.";
        }
        else
        {
            TempData["Success"] = "Quantity adjusted.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _inventory.DeleteAsync(id);
        TempData["Success"] = "Inventory item deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdownsAsync()
    {
        ViewBag.Products = new SelectList(await _products.GetAllAsync(), nameof(Product.Id), nameof(Product.Name));
        ViewBag.Warehouses = new SelectList(await _warehouses.GetAllAsync(), nameof(Warehouse.Id), nameof(Warehouse.Name));
    }
}
