using AzureInventoryPlatform.Contracts.Models;

namespace AzureInventoryPlatform.Web.ApiClients;

public interface IInventoryApiClient
{
    Task<IReadOnlyList<InventoryItem>> GetAllAsync();
    Task<InventoryItem?> GetByIdAsync(int id);
    Task<(bool Success, string? Error)> CreateAsync(InventoryItem item);
    Task<(bool Success, string? Error)> AdjustAsync(int id, int delta);
    Task DeleteAsync(int id);
}
