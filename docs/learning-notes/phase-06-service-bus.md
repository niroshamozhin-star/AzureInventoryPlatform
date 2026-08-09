# Phase 6 — Service Bus

## AZ-204 objectives covered
- Develop message-based solutions (Service Bus queues)

## 1. Business scenario

Right now, adjusting an inventory quantity on the site does exactly one
thing: update the row in Azure SQL, show a success message, done. This
phase adds a decoupled side-channel on top of that, without changing that
core behavior at all.

**What this is:** an *event notification* pattern. The database write
happens synchronously and immediately, exactly as before. Only *after* it
succeeds does the app publish a message saying "this already happened",
for anything else to react to independently.

**What this deliberately isn't:** a *command queue* / write-behind
pattern, where the click would drop a message on the queue first and a
worker would perform the actual database write moments later. That pattern
is real (used for load-leveling database writes under heavy burst traffic),
but it doesn't fit this feature — one person clicking "Adjust" on one row
is not a load problem Azure SQL needs help with, and doing it anyway would
mean showing a "success" message before the update is actually true. The
pattern *does* fit a different feature already in this app (bulk Excel
import, which can genuinely be slow enough to want queuing), but that's a
future decision, not part of this phase.

**The actual value:** new reactions to an inventory change - emailing a
manager, syncing another system - can be added later purely by plugging a
new listener onto the existing queue, without ever touching
`InventoryController` again. Right now there's exactly one listener, and
it only logs, to prove the mechanism fires end to end.

## 2. Azure resource created

**Service Bus namespace** `niro-inventory-servicebus`, **Basic** tier
(cheapest — Basic supports queues, which is all this phase needs; Standard
adds topics/subscriptions, not required here). One queue, `inventory-events`,
default settings (14-day message TTL, 10 max delivery count).

## 3. Code

- **`Data/InventoryEventPublisher.cs`** (Web project) — wraps
  `ServiceBusClient`/`ServiceBusSender`. Connects via
  `new ServiceBusClient(namespace, new DefaultAzureCredential())` -
  passwordless, same managed-identity pattern as Phase 8/9. If
  `ServiceBus:Namespace` isn't configured, publishing is a silent no-op
  rather than a startup failure - this is a side effect of the core flow,
  not a dependency of it.
- **`InventoryController.Adjust` (POST)** — after `_inventory.UpdateAsync(item)`
  succeeds, calls the publisher inside its own `try/catch`. A Service Bus
  outage logs a warning but does **not** fail the user's request - the
  queue is additive, not load-bearing.
- **`InventoryEventFunction.cs`** (Functions project) — a
  `[ServiceBusTrigger("inventory-events", Connection = "ServiceBusConnection")]`
  function that logs whatever message arrives. Deliberately minimal, same
  scope as `LowStockTimerFunction` from Phase 5 - it proves the trigger
  fires, nothing more yet.

## 4. Azure Portal configuration

1. Create the Service Bus namespace (Basic, same resource group/region as
   everything else).
2. Namespace → **Queues** → create `inventory-events`.
3. Namespace → **Access control (IAM)** → Add role assignment →
   **Azure Service Bus Data Sender** → Managed identity → the Web App
   (it only publishes, so it only needs send rights).
4. Function App → **Identity** → System-assigned → **On** (it didn't have
   one yet - Key Vault/Managed Identity work only touched the Web App).
5. Namespace → IAM → Add role assignment → **Azure Service Bus Data Receiver**
   → Managed identity → the Function App (it only consumes, so it gets a
   narrower role than the Web App).
6. Web App → Configuration → Application settings: add
   `ServiceBus__Namespace` = `niro-inventory-servicebus.servicebus.windows.net`.
7. Function App → Configuration → Application settings: add
   `ServiceBusConnection__fullyQualifiedNamespace` = same value - a
   different setting *name* than the Web App's, since this one follows the
   Functions runtime's own naming convention for identity-based trigger
   connections rather than a plain app setting the code reads directly.

## 5. Testing

**Verified live, full round trip:** published both projects, then adjusted
a real product's quantity (+5) on the live site. Within seconds, the
Function App's Log stream showed `InventoryEventFunction` firing,
receiving the exact message published - matching `InventoryId`, `ProductId`,
`WarehouseId`, `Delta`, and `NewQuantity` against what was just submitted
on the site - and completing successfully. Confirms the whole chain works
end to end, independently of the page that triggered it: the user's
request already finished and returned before the Function even ran.

---
**Previous phase:** [Phase 8 & 9 — Key Vault and Managed Identity](phase-08-09-key-vault-managed-identity.md)
**Next phase:** Phase 7 — Cosmos DB *(drafted, not yet deployed/tested)*
