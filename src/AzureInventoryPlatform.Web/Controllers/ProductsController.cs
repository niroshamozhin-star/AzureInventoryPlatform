using AzureInventoryPlatform.Web.Data;
using AzureInventoryPlatform.Web.Models;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Web.Controllers;

[Authorize]
public class ProductsController : Controller
{
    private readonly ProductData _products;
    private readonly ProductImageStorage _images;
    private readonly TelemetryClient _telemetry;

    public ProductsController(ProductData products, ProductImageStorage images, TelemetryClient telemetry)
    {
        _products = products;
        _images = images;
        _telemetry = telemetry;
    }

    public async Task<IActionResult> Index() => View(await _products.GetAllAsync());

    public IActionResult Create() => View(new Product());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
    {
        if (!ModelState.IsValid)
        {
            return View(product);
        }

        if (imageFile is { Length: > 0 })
        {
            product.ImageUrl = await _images.UploadAsync(imageFile);
        }

        await _products.AddAsync(product);

        // Phase 10: a custom event on top of the automatic request/dependency
        // tracking AddApplicationInsightsTelemetry() already gives every
        // action for free - this one is deliberate business telemetry, not
        // just "a request happened".
        _telemetry.TrackEvent("ProductCreated", new Dictionary<string, string>
        {
            ["ProductCode"] = product.ProductCode,
            ["HasImage"] = (imageFile is { Length: > 0 }).ToString(),
        });

        TempData["Success"] = $"Product \"{product.ProductName}\" created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var product = await _products.GetByIdAsync(id);
        return product is null ? NotFound() : View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Product product, IFormFile? imageFile)
    {
        if (id != product.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(product);
        }

        if (imageFile is { Length: > 0 })
        {
            // product.ImageUrl still holds the *old* URL here (carried forward by
            // the Edit view's hidden field) - delete that blob before overwriting
            // it, so replacing an image doesn't leave the old one as an orphan.
            if (!string.IsNullOrEmpty(product.ImageUrl))
            {
                await _images.DeleteAsync(product.ImageUrl);
            }

            product.ImageUrl = await _images.UploadAsync(imageFile);
        }

        await _products.UpdateAsync(product);
        TempData["Success"] = $"Product \"{product.ProductName}\" updated.";
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
        var product = await _products.GetByIdAsync(id);
        if (product is { ImageUrl: not null })
        {
            await _images.DeleteAsync(product.ImageUrl);
        }

        await _products.DeleteAsync(id);
        TempData["Success"] = "Product deleted.";
        return RedirectToAction(nameof(Index));
    }
}
