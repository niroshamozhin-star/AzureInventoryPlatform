namespace AzureInventoryPlatform.Web.ViewModels;

public record InventoryListItem(
    int Id,
    int ProductId,
    string ProductName,
    int WarehouseId,
    string WarehouseName,
    int Quantity,
    int ReorderLevel,
    DateTime LastUpdated)
{
    public bool IsLowStock => Quantity <= ReorderLevel;
}
