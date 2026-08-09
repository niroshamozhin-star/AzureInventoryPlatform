using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureInventoryPlatform.Functions;

// Phase 6: the consumer side of the Service Bus queue the Web app publishes
// to whenever an inventory quantity is adjusted (see InventoryEventPublisher
// in AzureInventoryPlatform.Web). Kept as a plain log line, same "prove the
// trigger actually fires" scope as LowStockTimerFunction, rather than doing
// anything with the event yet.
public class InventoryEventFunction
{
    private readonly ILogger<InventoryEventFunction> _logger;

    public InventoryEventFunction(ILogger<InventoryEventFunction> logger)
    {
        _logger = logger;
    }

    [Function("InventoryEventFunction")]
    public void Run([ServiceBusTrigger("inventory-events", Connection = "ServiceBusConnection")] string message)
    {
        _logger.LogInformation("Inventory event received: {Message}", message);
    }
}
