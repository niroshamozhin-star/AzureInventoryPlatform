using System.Net.Http.Json;
using AzureInventoryPlatform.Contracts.Models;

namespace AzureInventoryPlatform.Web.ApiClients;

public class ReportApiClient : IReportApiClient
{
    private readonly HttpClient _http;

    public ReportApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<WarehouseStockSummary>> GetStockSummaryAsync() =>
        await _http.GetFromJsonAsync<List<WarehouseStockSummary>>("api/reports/stock-summary") ?? [];

    public async Task<IReadOnlyList<LowStockAlert>> GetLowStockAsync() =>
        await _http.GetFromJsonAsync<List<LowStockAlert>>("api/reports/low-stock") ?? [];
}
