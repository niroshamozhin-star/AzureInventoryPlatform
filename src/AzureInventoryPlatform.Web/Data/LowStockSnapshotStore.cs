using Microsoft.Azure.Cosmos;

namespace AzureInventoryPlatform.Web.Data;

public record LowStockSnapshot(int LowStockCount, DateTime CheckedAtUtc);

// Phase 7: reads the "latest" low-stock snapshot that the Functions app's
// timer trigger writes to Cosmos DB, instead of the Reports page always
// re-running the SQL join itself. Registered as a singleton like
// ProductImageStorage - CosmosClient is documented as safe to reuse.
public class LowStockSnapshotStore
{
    private readonly CosmosClient? _client;
    private readonly string _databaseName;
    private readonly string _containerName;

    public LowStockSnapshotStore(IConfiguration configuration)
    {
        var connectionString = configuration["Cosmos:ConnectionString"];
        _databaseName = configuration["Cosmos:DatabaseName"] ?? "InventoryAnalytics";
        _containerName = configuration["Cosmos:ContainerName"] ?? "LowStockSnapshots";

        // Not configured yet (e.g. before Portal setup, or local dev) -
        // callers get null back instead of a startup crash.
        _client = string.IsNullOrWhiteSpace(connectionString) ? null : new CosmosClient(connectionString);
    }

    public async Task<LowStockSnapshot?> GetLatestAsync()
    {
        if (_client is null)
        {
            return null;
        }

        try
        {
            var container = _client.GetContainer(_databaseName, _containerName);
            var response = await container.ReadItemAsync<dynamic>("latest", new PartitionKey("latest"));
            var doc = response.Resource;
            return new LowStockSnapshot((int)doc.LowStockCount, (DateTime)doc.CheckedAtUtc);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception)
        {
            // Cosmos is a read-side cache here, not a dependency the Reports
            // page should ever 500 over - misconfiguration just means no
            // cached snapshot is shown.
            return null;
        }
    }
}
