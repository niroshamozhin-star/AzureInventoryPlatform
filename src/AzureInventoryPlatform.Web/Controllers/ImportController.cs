using System.Globalization;
using AzureInventoryPlatform.Web.Data;
using AzureInventoryPlatform.Web.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Web.Controllers;

public class ImportController : Controller
{
    private readonly ProductData _products;
    private readonly WarehouseData _warehouses;
    private readonly InventoryData _inventory;

    public ImportController(ProductData products, WarehouseData warehouses, InventoryData inventory)
    {
        _products = products;
        _warehouses = warehouses;
        _inventory = inventory;
    }

    public IActionResult Index() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Index(IFormFile? productsFile, IFormFile? warehousesFile, IFormFile? inventoryFile)
    {
        if (productsFile is not { Length: > 0 } || warehousesFile is not { Length: > 0 } || inventoryFile is not { Length: > 0 })
        {
            TempData["Error"] = "Please choose all three files (Products, Warehouses, Inventory) before importing.";
            return RedirectToAction(nameof(Index));
        }

        // ProductCode/WarehouseCode only exist in the spreadsheets - the database
        // tables have no code column - so the mapping to real Ids only needs to
        // live in memory for the life of this one import request.
        var productCodeToReorderLevel = new Dictionary<string, int>();
        var productCodeToId = new Dictionary<string, int>();
        var warehouseCodeToId = new Dictionary<string, int>();

        var productRows = ReadRows(productsFile);
        foreach (var row in productRows)
        {
            var code = row[1];
            var product = await _products.AddAsync(new Product
            {
                Sku = code,
                Name = row[2],
                UnitPrice = decimal.Parse(row[3], CultureInfo.InvariantCulture),
            });
            productCodeToId[code] = product.Id;
            productCodeToReorderLevel[code] = int.Parse(row[4], CultureInfo.InvariantCulture);
        }

        var warehouseRows = ReadRows(warehousesFile);
        foreach (var row in warehouseRows)
        {
            var code = row[1];
            var warehouse = await _warehouses.AddAsync(new Warehouse
            {
                Name = row[2],
                Location = row[3],
            });
            warehouseCodeToId[code] = warehouse.Id;
        }

        var inventoryRows = ReadRows(inventoryFile);
        var skipped = 0;
        foreach (var row in inventoryRows)
        {
            var productCode = row[1];
            var warehouseCode = row[2];

            if (!productCodeToId.TryGetValue(productCode, out var productId) ||
                !warehouseCodeToId.TryGetValue(warehouseCode, out var warehouseId))
            {
                skipped++;
                continue;
            }

            await _inventory.AddAsync(new InventoryItem
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                QuantityOnHand = int.Parse(row[3], CultureInfo.InvariantCulture),
                ReorderLevel = productCodeToReorderLevel[productCode],
            });
        }

        TempData["Success"] =
            $"Imported {productRows.Count} products, {warehouseRows.Count} warehouses, " +
            $"{inventoryRows.Count - skipped} inventory records" +
            (skipped > 0 ? $" ({skipped} inventory rows skipped - unknown product/warehouse code)." : ".");
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
