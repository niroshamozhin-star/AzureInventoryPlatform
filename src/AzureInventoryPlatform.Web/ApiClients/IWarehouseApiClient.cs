using AzureInventoryPlatform.Contracts.Models;

namespace AzureInventoryPlatform.Web.ApiClients;

public interface IWarehouseApiClient
{
    Task<IReadOnlyList<Warehouse>> GetAllAsync();
    Task<Warehouse?> GetByIdAsync(int id);
    Task CreateAsync(Warehouse warehouse);
    Task UpdateAsync(Warehouse warehouse);
    Task DeleteAsync(int id);
}
