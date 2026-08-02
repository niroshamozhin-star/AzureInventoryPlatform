using AzureInventoryPlatform.Api.Models;

namespace AzureInventoryPlatform.Web.ApiClients;

public interface IReportApiClient
{
    Task<IReadOnlyList<WarehouseStockSummary>> GetStockSummaryAsync();
    Task<IReadOnlyList<LowStockAlert>> GetLowStockAsync();
}
