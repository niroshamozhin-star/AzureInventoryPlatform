using AzureInventoryPlatform.Web.ApiClients;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Web.Controllers;

public class ReportsController : Controller
{
    private readonly IReportApiClient _reports;

    public ReportsController(IReportApiClient reports)
    {
        _reports = reports;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.LowStock = await _reports.GetLowStockAsync();
        return View(await _reports.GetStockSummaryAsync());
    }
}
