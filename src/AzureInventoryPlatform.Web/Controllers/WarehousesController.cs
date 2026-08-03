using AzureInventoryPlatform.Web.Data;
using AzureInventoryPlatform.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Web.Controllers;

[Authorize]
public class WarehousesController : Controller
{
    private readonly WarehouseData _warehouses;

    public WarehousesController(WarehouseData warehouses)
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

        await _warehouses.AddAsync(warehouse);
        TempData["Success"] = $"Warehouse \"{warehouse.WarehouseName}\" created.";
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
        TempData["Success"] = $"Warehouse \"{warehouse.WarehouseName}\" updated.";
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
