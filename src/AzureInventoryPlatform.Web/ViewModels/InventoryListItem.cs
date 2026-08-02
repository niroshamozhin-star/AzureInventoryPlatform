namespace AzureInventoryPlatform.Web.ViewModels;

public record InventoryListItem(
    int Id,
    int ProductId,
    string ProductName,
    int WarehouseId,
    string WarehouseName,
    int QuantityOnHand,
    int ReorderLevel)
{
    public bool IsLowStock => QuantityOnHand <= ReorderLevel;
}
