using Microsoft.Azure.Cosmos;
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

        await WriteCosmosSnapshotAsync(lowStockCount);
    }

    // Phase 7: writes the same count into Cosmos DB as a denormalized
    // "latest" snapshot - the Web app's report page reads this instead of
    // re-running the SQL join every time someone opens it. Cosmos:ConnectionString
    // not configured yet (e.g. before Portal setup, or local dev) - skip
    // rather than fail the whole timer run over an optional side effect.
    private async Task WriteCosmosSnapshotAsync(int lowStockCount)
    {
        var connectionString = _configuration["Cosmos:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var databaseName = _configuration["Cosmos:DatabaseName"] ?? "InventoryAnalytics";
        var containerName = _configuration["Cosmos:ContainerName"] ?? "LowStockSnapshots";

        try
        {
            using var client = new CosmosClient(connectionString);
            var container = client.GetContainer(databaseName, containerName);
            await container.UpsertItemAsync(new LowStockSnapshot("latest", lowStockCount, DateTime.UtcNow), new PartitionKey("latest"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write low-stock snapshot to Cosmos DB.");
        }
    }

    private record LowStockSnapshot(string id, int LowStockCount, DateTime CheckedAtUtc);
}
