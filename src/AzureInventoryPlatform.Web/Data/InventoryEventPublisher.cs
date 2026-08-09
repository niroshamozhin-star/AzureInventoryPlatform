using System.Text.Json;
using Azure.Identity;
using Azure.Messaging.ServiceBus;

namespace AzureInventoryPlatform.Web.Data;

// Phase 6: fires a Service Bus message whenever an inventory quantity is
// adjusted, decoupling "update the row" from anything that reacts to the
// change (currently just the logging Function in AzureInventoryPlatform.Functions).
// Registered as a singleton like ProductImageStorage - ServiceBusClient is
// documented as safe to reuse across requests.
public class InventoryEventPublisher
{
    private readonly ServiceBusClient? _client;
    private readonly string _queueName;

    public InventoryEventPublisher(IConfiguration configuration)
    {
        var ns = configuration["ServiceBus:Namespace"];
        _queueName = configuration["ServiceBus:QueueName"] ?? "inventory-events";

        // No namespace configured (e.g. running locally without Service Bus) -
        // publishing becomes a no-op instead of a hard startup failure, since
        // this is a side effect of the core CRUD flow, not a dependency of it.
        _client = string.IsNullOrWhiteSpace(ns)
            ? null
            : new ServiceBusClient(ns, new DefaultAzureCredential());
    }

    public async Task PublishQuantityChangedAsync(int inventoryId, int productId, int warehouseId, int delta, int newQuantity)
    {
        if (_client is null)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            InventoryId = inventoryId,
            ProductId = productId,
            WarehouseId = warehouseId,
            Delta = delta,
            NewQuantity = newQuantity,
            ChangedAtUtc = DateTime.UtcNow,
        });

        var sender = _client.CreateSender(_queueName);
        await sender.SendMessageAsync(new ServiceBusMessage(payload));
    }
}
