using AzureInventoryPlatform.Web.Models;
using AzureInventoryPlatform.Web.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Web.Controllers;

public class WarehousesController : Controller
{
    private readonly IRepository<Warehouse> _warehouses;

    public WarehousesController(IRepository<Warehouse> warehouses)
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
