using AzureInventoryPlatform.Web.Models;

namespace AzureInventoryPlatform.Web.Repositories;

public interface IRepository<T> where T : IEntity
{
    Task<IReadOnlyList<T>> GetAllAsync();
    Task<T?> GetByIdAsync(int id);
    Task<T> AddAsync(T entity);
    Task<bool> UpdateAsync(T entity);
    Task<bool> DeleteAsync(int id);
}
