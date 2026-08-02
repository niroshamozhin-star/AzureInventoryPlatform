using AzureInventoryPlatform.Api.Models;

namespace AzureInventoryPlatform.Web.ApiClients;

public interface IProductApiClient
{
    Task<IReadOnlyList<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(int id);
    Task CreateAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(int id);
}
