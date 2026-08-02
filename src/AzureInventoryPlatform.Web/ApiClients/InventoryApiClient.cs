using System.Net.Http.Json;
using AzureInventoryPlatform.Api.Models;

namespace AzureInventoryPlatform.Web.ApiClients;

public class InventoryApiClient : IInventoryApiClient
{
    private readonly HttpClient _http;

    public InventoryApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<InventoryItem>> GetAllAsync() =>
        await _http.GetFromJsonAsync<List<InventoryItem>>("api/inventory") ?? [];

    public async Task<InventoryItem?> GetByIdAsync(int id)
    {
        var response = await _http.GetAsync($"api/inventory/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<InventoryItem>();
    }

    public async Task<(bool Success, string? Error)> CreateAsync(InventoryItem item)
    {
        var response = await _http.PostAsJsonAsync("api/inventory", item);
        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task<(bool Success, string? Error)> AdjustAsync(int id, int delta)
    {
        var response = await _http.PatchAsJsonAsync($"api/inventory/{id}/adjust", delta);
        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        return (false, await response.Content.ReadAsStringAsync());
    }

    public async Task DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/inventory/{id}");
        response.EnsureSuccessStatusCode();
    }
}
