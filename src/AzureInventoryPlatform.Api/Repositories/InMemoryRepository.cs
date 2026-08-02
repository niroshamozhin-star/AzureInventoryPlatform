using System.Collections.Concurrent;
using AzureInventoryPlatform.Api.Models;

namespace AzureInventoryPlatform.Api.Repositories;

/// <summary>
/// Thread-safe in-memory store. Placeholder for Phase 2 (Azure SQL/EF Core) and
/// Phase 6 (Cosmos DB), which will provide alternate IRepository&lt;T&gt; implementations.
/// </summary>
public class InMemoryRepository<T> : IRepository<T> where T : class, IEntity
{
    private readonly ConcurrentDictionary<int, T> _items = new();
    private int _nextId;

    public Task<IReadOnlyList<T>> GetAllAsync() =>
        Task.FromResult((IReadOnlyList<T>)_items.Values.ToList());

    public Task<T?> GetByIdAsync(int id) =>
        Task.FromResult(_items.TryGetValue(id, out var item) ? item : null);

    public Task<T> AddAsync(T entity)
    {
        entity.Id = Interlocked.Increment(ref _nextId);
        _items[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task<bool> UpdateAsync(T entity)
    {
        if (!_items.ContainsKey(entity.Id))
        {
            return Task.FromResult(false);
        }

        _items[entity.Id] = entity;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int id) => Task.FromResult(_items.TryRemove(id, out _));
}
