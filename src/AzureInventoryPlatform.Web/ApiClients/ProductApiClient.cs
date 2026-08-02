using System.Net.Http.Json;
using AzureInventoryPlatform.Api.Models;

namespace AzureInventoryPlatform.Web.ApiClients;

public class ProductApiClient : IProductApiClient
{
    private readonly HttpClient _http;

    public ProductApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync() =>
        await _http.GetFromJsonAsync<List<Product>>("api/products") ?? [];

    public async Task<Product?> GetByIdAsync(int id)
    {
        var response = await _http.GetAsync($"api/products/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<Product>();
    }

    public async Task CreateAsync(Product product)
    {
        var response = await _http.PostAsJsonAsync("api/products", product);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(Product product)
    {
        var response = await _http.PutAsJsonAsync($"api/products/{product.Id}", product);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/products/{id}");
        response.EnsureSuccessStatusCode();
    }
}
