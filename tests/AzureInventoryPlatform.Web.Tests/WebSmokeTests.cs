using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AzureInventoryPlatform.Web.Tests;

public class WebSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public WebSmokeTests(WebApplicationFactory<Program> factory)
    {
        // Fixed, test-only login credentials, independent of the developer's
        // local User Secrets, so this suite passes the same way on any
        // machine or in CI.
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:Username"] = "testuser",
                    ["Auth:Password"] = "testpass",
                });
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        // Health checks are not behind [Authorize] - Azure/monitoring probes
        // must reach this with no login.
        var response = await _client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnauthenticatedRequest_RedirectsToLogin()
    {
        var response = await _client.GetAsync("/");

        Assert.Contains("/Account/Login", response.RequestMessage!.RequestUri!.ToString());
        Assert.Contains("Log In", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShowsError()
    {
        var response = await LoginAsync(_client, "testuser", "wrong-password");

        Assert.Contains("Invalid username or password", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_GrantsAccessToDashboard()
    {
        await LoginAsync(_client, "testuser", "testpass");

        var response = await _client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        Assert.DoesNotContain("/Account/Login", response.RequestMessage!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Logout_ThenDashboard_RedirectsBackToLogin()
    {
        await LoginAsync(_client, "testuser", "testpass");

        // The Logout button's antiforgery token only renders in the nav once
        // authenticated, so pull it from the now-accessible dashboard.
        var dashboard = await _client.GetAsync("/");
        var logoutToken = ExtractAntiForgeryToken(await dashboard.Content.ReadAsStringAsync());

        await _client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = logoutToken,
        }));

        var response = await _client.GetAsync("/");
        Assert.Contains("/Account/Login", response.RequestMessage!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Products_Index_ShowsSeedData()
    {
        await LoginAsync(_client, "testuser", "testpass");

        var html = await _client.GetStringAsync("/Products");

        Assert.Contains("WID-001", html);
        Assert.Contains("GAD-002", html);
    }

    [Fact]
    public async Task Warehouses_Index_ShowsSeedData()
    {
        await LoginAsync(_client, "testuser", "testpass");

        var html = await _client.GetStringAsync("/Warehouses");

        Assert.Contains("East DC", html);
        Assert.Contains("West DC", html);
    }

    [Fact]
    public async Task Inventory_Index_FlagsSeededGadgetShortage()
    {
        await LoginAsync(_client, "testuser", "testpass");

        var html = await _client.GetStringAsync("/Inventory");

        Assert.Contains("Gadget", html);
        Assert.Contains("Low stock", html);
    }

    [Fact]
    public async Task Reports_Index_RedirectsToInventoryByWarehouse()
    {
        await LoginAsync(_client, "testuser", "testpass");

        // /Reports has no view of its own - it's a default landing redirect
        // to the first of the two report sub-pages.
        var html = await _client.GetStringAsync("/Reports");

        Assert.Contains("Inventory by Warehouse", html);
        Assert.Contains("East DC", html);
    }

    [Fact]
    public async Task Reports_LowStock_ShowsSeededGadgetShortage()
    {
        await LoginAsync(_client, "testuser", "testpass");

        var html = await _client.GetStringAsync("/Reports/LowStock");

        Assert.Contains("Gadget", html);
    }

    [Fact]
    public async Task Inventory_Create_WithUnknownProduct_ReturnsFormWithValidationError()
    {
        await LoginAsync(_client, "testuser", "testpass");

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

    private async Task<HttpResponseMessage> LoginAsync(HttpClient client, string username, string password)
    {
        var loginPage = await client.GetAsync("/Account/Login");
        var token = ExtractAntiForgeryToken(await loginPage.Content.ReadAsStringAsync());

        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["username"] = username,
            ["password"] = password,
        }));
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        return match.Groups[1].Value;
    }
}
