namespace AzureInventoryPlatform.Web.ViewModels;

public record DashboardViewModel(int ProductCount, int WarehouseCount, int InventoryItemCount, int LowStockCount);
