using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AzureInventoryPlatform.Functions;

public class LowStockTimerFunction
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LowStockTimerFunction> _logger;

    public LowStockTimerFunction(IConfiguration configuration, ILogger<LowStockTimerFunction> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    // Fires on a schedule, not because anyone called it - no HTTP request,
    // no blob upload, just the Functions runtime waking this up every 5
    // minutes. Same low-stock query as GetLowStockReport, but this one logs
    // the result instead of returning it to a caller.
    [Function("LowStockTimerCheck")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
    {
        var connectionString = _configuration["SqlConnectionString"]
            ?? throw new InvalidOperationException("Missing SqlConnectionString configuration.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            """
            SELECT COUNT(*)
            FROM dbo.Inventory i
            JOIN dbo.Products p ON p.ProductId = i.ProductId
            WHERE i.Quantity <= p.ReorderLevel
            """, connection);

        var lowStockCount = (int)(await command.ExecuteScalarAsync() ?? 0);

        _logger.LogInformation("Scheduled low-stock check: {Count} item(s) at or below reorder level.", lowStockCount);
    }
}
