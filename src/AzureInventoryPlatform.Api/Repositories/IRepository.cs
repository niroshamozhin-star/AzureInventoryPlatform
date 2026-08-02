using AzureInventoryPlatform.Api.Models;

namespace AzureInventoryPlatform.Api.Repositories;

public interface IRepository<T> where T : IEntity
{
    Task<IReadOnlyList<T>> GetAllAsync();
    Task<T?> GetByIdAsync(int id);
    Task<T> AddAsync(T entity);
    Task<bool> UpdateAsync(T entity);
    Task<bool> DeleteAsync(int id);
}
