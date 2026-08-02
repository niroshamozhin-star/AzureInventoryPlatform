using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AzureInventoryPlatform.Web.Tests;

public class WebSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public WebSmokeTests(WebApplicationFactory<Program> factory)
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
    public async Task Dashboard_Loads()
    {
        var response = await _client.GetAsync("/");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Products_Index_ShowsSeedData()
    {
        var html = await _client.GetStringAsync("/Products");

        Assert.Contains("WID-001", html);
        Assert.Contains("GAD-002", html);
    }

    [Fact]
    public async Task Warehouses_Index_ShowsSeedData()
    {
        var html = await _client.GetStringAsync("/Warehouses");

        Assert.Contains("East DC", html);
        Assert.Contains("West DC", html);
    }

    [Fact]
    public async Task Inventory_Index_FlagsSeededGadgetShortage()
    {
        var html = await _client.GetStringAsync("/Inventory");

        Assert.Contains("Gadget", html);
        Assert.Contains("Low stock", html);
    }

    [Fact]
    public async Task Reports_Index_ShowsLowStockAlert()
    {
        var html = await _client.GetStringAsync("/Reports");

        Assert.Contains("Gadget", html);
        Assert.Contains("East DC", html);
    }

    [Fact]
    public async Task Inventory_Create_WithUnknownProduct_ReturnsFormWithValidationError()
    {
        var formPage = await _client.GetAsync("/Inventory/Create");
        var token = ExtractAntiForgeryToken(await formPage.Content.ReadAsStringAsync());

        var response = await _client.PostAsync("/Inventory/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ProductId"] = "9999",
            ["WarehouseId"] = "1",
            ["Quantity"] = "10",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("does not exist", await response.Content.ReadAsStringAsync());
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        return match.Groups[1].Value;
    }
}
