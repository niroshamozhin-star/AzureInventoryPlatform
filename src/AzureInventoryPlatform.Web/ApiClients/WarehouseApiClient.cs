using System.Net.Http.Json;
using AzureInventoryPlatform.Contracts.Models;

namespace AzureInventoryPlatform.Web.ApiClients;

public class WarehouseApiClient : IWarehouseApiClient
{
    private readonly HttpClient _http;

    public WarehouseApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<Warehouse>> GetAllAsync() =>
        await _http.GetFromJsonAsync<List<Warehouse>>("api/warehouses") ?? [];

    public async Task<Warehouse?> GetByIdAsync(int id)
    {
        var response = await _http.GetAsync($"api/warehouses/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<Warehouse>();
    }

    public async Task CreateAsync(Warehouse warehouse)
    {
        var response = await _http.PostAsJsonAsync("api/warehouses", warehouse);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(Warehouse warehouse)
    {
        var response = await _http.PutAsJsonAsync($"api/warehouses/{warehouse.Id}", warehouse);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/warehouses/{id}");
        response.EnsureSuccessStatusCode();
    }
}
