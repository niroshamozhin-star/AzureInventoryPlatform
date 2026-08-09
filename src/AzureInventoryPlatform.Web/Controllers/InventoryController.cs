using AzureInventoryPlatform.Web.Data;
using AzureInventoryPlatform.Web.Models;
using AzureInventoryPlatform.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AzureInventoryPlatform.Web.Controllers;

[Authorize]
public class InventoryController : Controller
{
    private readonly InventoryData _inventory;
    private readonly ProductData _products;
    private readonly WarehouseData _warehouses;
    private readonly InventoryEventPublisher _events;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        InventoryData inventory,
        ProductData products,
        WarehouseData warehouses,
        InventoryEventPublisher events,
        ILogger<InventoryController> logger)
    {
        _inventory = inventory;
        _products = products;
        _warehouses = warehouses;
        _events = events;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var items = await _inventory.GetAllAsync();
        var products = (await _products.GetAllAsync()).ToDictionary(p => p.Id);
        var warehouses = (await _warehouses.GetAllAsync()).ToDictionary(w => w.Id);

        var viewModel = items
            .Select(i =>
            {
                products.TryGetValue(i.ProductId, out var product);
                warehouses.TryGetValue(i.WarehouseId, out var warehouse);
                return new InventoryListItem(
                    i.Id,
                    i.ProductId,
                    product?.ProductName ?? "(unknown product)",
                    i.WarehouseId,
                    warehouse?.WarehouseName ?? "(unknown warehouse)",
                    i.Quantity,
                    product?.ReorderLevel ?? 0,
                    i.LastUpdated);
            })
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
        if (await _products.GetByIdAsync(item.ProductId) is null)
        {
            ModelState.AddModelError(string.Empty, $"Product {item.ProductId} does not exist.");
        }

        if (await _warehouses.GetByIdAsync(item.WarehouseId) is null)
        {
            ModelState.AddModelError(string.Empty, $"Warehouse {item.WarehouseId} does not exist.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return View(item);
        }

        await _inventory.AddAsync(item);
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
        ViewBag.ProductName = product?.ProductName ?? "(unknown product)";
        ViewBag.WarehouseName = warehouse?.WarehouseName ?? "(unknown warehouse)";
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(int id, int delta)
    {
        var item = await _inventory.GetByIdAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        var newQuantity = item.Quantity + delta;
        if (newQuantity < 0)
        {
            TempData["Error"] = "Adjustment would result in negative quantity on hand.";
        }
        else
        {
            item.Quantity = newQuantity;
            await _inventory.UpdateAsync(item);
            TempData["Success"] = "Quantity adjusted.";

            // Service Bus is a side channel, not part of the core update - a
            // queue outage shouldn't stop the user from adjusting inventory.
            try
            {
                await _events.PublishQuantityChangedAsync(item.Id, item.ProductId, item.WarehouseId, delta, item.Quantity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish inventory-changed event for InventoryId={InventoryId}", item.Id);
            }
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
        ViewBag.Products = new SelectList(await _products.GetAllAsync(), nameof(Product.Id), nameof(Product.ProductName));
        ViewBag.Warehouses = new SelectList(await _warehouses.GetAllAsync(), nameof(Warehouse.Id), nameof(Warehouse.WarehouseName));
    }
}
