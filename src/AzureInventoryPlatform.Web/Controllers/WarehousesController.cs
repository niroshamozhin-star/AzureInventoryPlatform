using AzureInventoryPlatform.Api.Models;
using AzureInventoryPlatform.Web.ApiClients;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Web.Controllers;

public class WarehousesController : Controller
{
    private readonly IWarehouseApiClient _warehouses;

    public WarehousesController(IWarehouseApiClient warehouses)
    {
        _warehouses = warehouses;
    }

    public async Task<IActionResult> Index() => View(await _warehouses.GetAllAsync());

    public IActionResult Create() => View(new Warehouse());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Warehouse warehouse)
    {
        if (!ModelState.IsValid)
        {
            return View(warehouse);
        }

        await _warehouses.CreateAsync(warehouse);
        TempData["Success"] = $"Warehouse \"{warehouse.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var warehouse = await _warehouses.GetByIdAsync(id);
        return warehouse is null ? NotFound() : View(warehouse);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Warehouse warehouse)
    {
        if (id != warehouse.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(warehouse);
        }

        await _warehouses.UpdateAsync(warehouse);
        TempData["Success"] = $"Warehouse \"{warehouse.Name}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var warehouse = await _warehouses.GetByIdAsync(id);
        return warehouse is null ? NotFound() : View(warehouse);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _warehouses.DeleteAsync(id);
        TempData["Success"] = "Warehouse deleted.";
        return RedirectToAction(nameof(Index));
    }
}
