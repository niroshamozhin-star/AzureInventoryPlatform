using System.Globalization;
using AzureInventoryPlatform.Web.Data;
using AzureInventoryPlatform.Web.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Web.Controllers;

[Authorize]
public class ImportController : Controller
{
    private readonly ProductData _products;
    private readonly WarehouseData _warehouses;
    private readonly InventoryData _inventory;
    private readonly ProductImageStorage _images;

    public ImportController(ProductData products, WarehouseData warehouses, InventoryData inventory, ProductImageStorage images)
    {
        _products = products;
        _warehouses = warehouses;
        _inventory = inventory;
        _images = images;
    }

    public IActionResult Index() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Index(
        IFormFile? productsFile, IFormFile? warehousesFile, IFormFile? inventoryFile, List<IFormFile>? imageFiles)
    {
        if (productsFile is not { Length: > 0 } && warehousesFile is not { Length: > 0 } && inventoryFile is not { Length: > 0 })
        {
            TempData["Error"] = "Please choose at least one file to import.";
            return RedirectToAction(nameof(Index));
        }

        // Seeded from every product/warehouse already in the database, not just
        // ones inserted during this request - so Inventory.xlsx can be imported
        // on its own and still resolve codes for products/warehouses that were
        // created in an earlier import, not just this one. Keyed to the full
        // Product (not just its Id) so an existing image URL can be looked up
        // and deleted before a replacement is attached.
        var productsByCode = (await _products.GetAllAsync()).ToDictionary(p => p.ProductCode);
        var warehouseCodeToId = (await _warehouses.GetAllAsync()).ToDictionary(w => w.WarehouseCode, w => w.Id);
        var imagesByFileName = (imageFiles ?? [])
            .Where(f => f.Length > 0)
            .ToDictionary(f => f.FileName, f => f, StringComparer.OrdinalIgnoreCase);

        var productsCreated = 0;
        var productsImageUpdated = 0;
        if (productsFile is { Length: > 0 })
        {
            foreach (var row in ReadRows(productsFile))
            {
                var code = row[1];

                string? imageUrl = null;
                if (row.Length > 5 && !string.IsNullOrWhiteSpace(row[5]) && imagesByFileName.TryGetValue(row[5], out var imageFile))
                {
                    imageUrl = await _images.UploadAsync(imageFile);
                }

                // A ProductCode that already exists is treated as "attach the image
                // to that product," never re-inserted - ProductCode is UNIQUE, so a
                // blind re-insert would fail, and there's no reason to touch a
                // product's other fields just to add a picture to it.
                if (productsByCode.TryGetValue(code, out var existingProduct))
                {
                    if (imageUrl is not null)
                    {
                        if (!string.IsNullOrEmpty(existingProduct.ImageUrl))
                        {
                            await _images.DeleteAsync(existingProduct.ImageUrl);
                        }

                        await _products.UpdateImageUrlAsync(existingProduct.Id, imageUrl);
                        productsImageUpdated++;
                    }
                    continue;
                }

                var product = await _products.AddAsync(new Product
                {
                    ProductCode = code,
                    ProductName = row[2],
                    UnitPrice = decimal.Parse(row[3], CultureInfo.InvariantCulture),
                    ReorderLevel = int.Parse(row[4], CultureInfo.InvariantCulture),
                    ImageUrl = imageUrl,
                });
                productsByCode[code] = product;
                productsCreated++;
            }
        }

        var warehousesCreated = 0;
        if (warehousesFile is { Length: > 0 })
        {
            foreach (var row in ReadRows(warehousesFile))
            {
                var code = row[1];
                if (warehouseCodeToId.ContainsKey(code))
                {
                    continue;
                }

                var warehouse = await _warehouses.AddAsync(new Warehouse
                {
                    WarehouseCode = code,
                    WarehouseName = row[2],
                    City = row[3],
                });
                warehouseCodeToId[code] = warehouse.Id;
                warehousesCreated++;
            }
        }

        var inventoryCreated = 0;
        var inventorySkipped = 0;
        if (inventoryFile is { Length: > 0 })
        {
            foreach (var row in ReadRows(inventoryFile))
            {
                var productCode = row[1];
                var warehouseCode = row[2];

                if (!productsByCode.TryGetValue(productCode, out var product) ||
                    !warehouseCodeToId.TryGetValue(warehouseCode, out var warehouseId))
                {
                    inventorySkipped++;
                    continue;
                }

                await _inventory.AddAsync(new InventoryItem
                {
                    ProductId = product.Id,
                    WarehouseId = warehouseId,
                    Quantity = int.Parse(row[3], CultureInfo.InvariantCulture),
                    LastUpdated = DateTime.Parse(row[4], CultureInfo.InvariantCulture),
                });
                inventoryCreated++;
            }
        }

        var summary = $"{productsCreated} products created, {productsImageUpdated} product images attached, " +
                      $"{warehousesCreated} warehouses created, {inventoryCreated} inventory records created" +
                      (inventorySkipped > 0 ? $" ({inventorySkipped} inventory rows skipped - unknown product/warehouse code)." : ".");
        TempData["Success"] = summary;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Reads every used row (after the header) into plain string arrays before the
    /// workbook/stream closes, so callers never touch ClosedXML objects after disposal.
    /// </summary>
    private static List<string[]> ReadRows(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);

        return worksheet.RangeUsed()!.RowsUsed()
            .Skip(1)
            .Select(row => row.Cells().Select(cell => cell.GetString()).ToArray())
            .ToList();
    }
}
