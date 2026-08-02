namespace AzureInventoryPlatform.Web.Models;

public record WarehouseStockSummary(int WarehouseId, string WarehouseName, int TotalUnits, decimal TotalValue);

public record LowStockAlert(
    int InventoryItemId,
    int ProductId,
    string ProductName,
    int WarehouseId,
    string WarehouseName,
    int Quantity,
    int ReorderLevel);
