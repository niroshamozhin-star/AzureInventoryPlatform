using System.Net;
using System.Net.Http.Json;
using AzureInventoryPlatform.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AzureInventoryPlatform.Api.Tests;

public class ApiSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiSmokeTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Products_SeedDataIsReturned()
    {
        var products = await _client.GetFromJsonAsync<List<Product>>("/api/products");

        Assert.NotNull(products);
        Assert.Contains(products!, p => p.Sku == "WID-001");
    }

    [Fact]
    public async Task Warehouses_SeedDataIsReturned()
    {
        var warehouses = await _client.GetFromJsonAsync<List<Warehouse>>("/api/warehouses");

        Assert.NotNull(warehouses);
        Assert.Equal(2, warehouses!.Count);
    }

    [Fact]
    public async Task Inventory_CreateWithUnknownProduct_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/inventory", new InventoryItem
        {
            ProductId = 9999,
            WarehouseId = 1,
            QuantityOnHand = 10,
            ReorderLevel = 5
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reports_LowStock_FlagsSeededGadgetShortage()
    {
        var alerts = await _client.GetFromJsonAsync<List<LowStockAlert>>("/api/reports/low-stock");

        Assert.NotNull(alerts);
        Assert.Contains(alerts!, a => a.ProductName == "Gadget" && a.WarehouseName == "East DC");
    }

    [Fact]
    public async Task Reports_StockSummary_AggregatesAcrossWarehouses()
    {
        var summary = await _client.GetFromJsonAsync<List<WarehouseStockSummary>>("/api/reports/stock-summary");

        Assert.NotNull(summary);
        Assert.Equal(2, summary!.Count);
        Assert.All(summary!, s => Assert.True(s.TotalUnits > 0));
    }
}
