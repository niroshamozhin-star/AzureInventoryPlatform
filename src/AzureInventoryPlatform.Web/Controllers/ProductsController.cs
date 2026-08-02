using AzureInventoryPlatform.Web.Data;
using AzureInventoryPlatform.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Web.Controllers;

public class ProductsController : Controller
{
    private readonly ProductData _products;

    public ProductsController(ProductData products)
    {
        _products = products;
    }

    public async Task<IActionResult> Index() => View(await _products.GetAllAsync());

    public IActionResult Create() => View(new Product());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product product)
    {
        if (!ModelState.IsValid)
        {
            return View(product);
        }

        await _products.AddAsync(product);
        TempData["Success"] = $"Product \"{product.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var product = await _products.GetByIdAsync(id);
        return product is null ? NotFound() : View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Product product)
    {
        if (id != product.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(product);
        }

        await _products.UpdateAsync(product);
        TempData["Success"] = $"Product \"{product.Name}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var product = await _products.GetByIdAsync(id);
        return product is null ? NotFound() : View(product);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _products.DeleteAsync(id);
        TempData["Success"] = "Product deleted.";
        return RedirectToAction(nameof(Index));
    }
}
