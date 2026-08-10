# Phase 7 — Cosmos DB

## AZ-204 objectives covered
- Implement solutions that use Azure Cosmos DB (NoSQL API)

## 1. Business scenario

The Reports → Low Stock page currently re-runs a full SQL join
(`Inventory` + `Products` + `Warehouses`) every single time anyone opens
it. This phase adds a denormalized, pre-computed copy of just the *count*
of low-stock items in Cosmos DB, written on a schedule by the same Timer
function from Phase 5 - the Reports page reads that cached copy alongside
its normal live query, as visible proof the cache is real and current.

This isn't a replacement for the SQL query (the table on the page is
still always live, straight from SQL) - it's a second, independent path
that happens to land in the same place, so the value of "read a
denormalized document instead of running a join" is demonstrable without
having to remove or risk anything that already worked.

## 2. Flow

```
WRITE SIDE (Functions app - runs every 5 minutes, no caller)
Timer fires -> SQL COUNT(*) query -> log it -> Cosmos UpsertItemAsync
                                                 { id: "latest",
                                                   LowStockCount: N,
                                                   CheckedAtUtc: now }
                                                 (always overwrites the
                                                  SAME document)

READ SIDE (Web app - runs whenever a user visits the page)
User opens Reports/LowStock -> ReportsController.LowStock()
    |-> Azure SQL (live join)      -> the real table on the page
    |-> Cosmos ReadItemAsync("latest") -> LowStockSnapshotStore
                                       -> one extra line above the table,
                                          only if the document exists

The two sides never talk to each other directly - they only meet at the
one Cosmos document. The write happens on its own schedule regardless of
whether anyone is looking; the read happens whenever a user loads the
page, regardless of whether the timer has run recently.
```

## 3. Azure resource created

**Cosmos DB account** `niro-inventory-cosmos`, **Azure Cosmos DB for
NoSQL** API, **Serverless** capacity mode (billed per-request instead of
provisioned RU/s - the right fit for a low-traffic learning project).
Region: **South India**, not Central India like every other resource -
Central India wasn't offered as an available region for a new Cosmos
account at creation time (a real, unpredictable per-region capacity
constraint, not a configuration mistake). One database `InventoryAnalytics`,
one container `LowStockSnapshots`, **partition key `/id`** - the code
always reads/writes a single document with `id: "latest"` and partitions
on that same value, so the partition key path has to match.

**Auth for this phase:** connection string (key-based), not managed
identity. Cosmos's data-plane RBAC exists but isn't as directly
GUI-configurable as Key Vault/Storage/Service Bus were in earlier phases -
kept simple here rather than fighting the Portal for a passwordless path
that would cost more setup time than it teaches for a learning project.

## 4. Code

- **`LowStockTimerFunction.cs`** (Functions project) - after logging the
  low-stock count (unchanged from Phase 5), calls
  `WriteCosmosSnapshotAsync(lowStockCount)`. Reads `Cosmos:ConnectionString`
  from config; if blank, returns immediately (no-op) rather than failing
  the whole timer run over an optional side effect. Otherwise opens a
  `CosmosClient`, gets the `LowStockSnapshots` container, and
  `UpsertItemAsync`s a record with `id: "latest"` - upsert means insert-or-
  overwrite, so this is always exactly one document, never a growing
  history. Any Cosmos failure is caught and logged as a warning, same
  "side effect, not load-bearing" pattern as Phase 6's Service Bus publish.
- **`Data/LowStockSnapshotStore.cs`** (Web project) - `GetLatestAsync()`
  does a point-read (`ReadItemAsync<dynamic>("latest", new PartitionKey("latest"))`)
  - the cheapest kind of Cosmos read, since it asks for one exact document
  by id + partition key rather than running any query. Returns `null` if
  Cosmos isn't configured yet, if the document doesn't exist yet (a 404
  `CosmosException`, caught explicitly), or on any other failure - this is
  a read-side cache, not a dependency the Reports page should ever 500 over.
- **`ReportsController.LowStock()`** - calls `_snapshots.GetLatestAsync()`
  and stuffs the result into `ViewBag.CosmosSnapshot`, leaving the
  action's actual `@model` (`IReadOnlyList<LowStockAlert>`) untouched.
- **`LowStock.cshtml`** - casts `ViewBag.CosmosSnapshot` back to
  `LowStockSnapshot?` and renders one line above the table only if it's
  not null. If Cosmos was never configured or the timer hasn't run yet,
  the page looks exactly like it did before this phase existed.

## 5. Azure Portal configuration

1. Create the Cosmos DB account (NoSQL API, Serverless, same resource
   group as everything else; region: whatever's actually available - see
   note above).
2. Data Explorer → **New Container** → Database id `InventoryAnalytics`
   (create new) → Container id `LowStockSnapshots` → Partition key `/id`.
3. Account → **Keys** → copy the **Primary Connection String**.
4. Function App → Configuration → Application settings → add
   `Cosmos__ConnectionString`.
5. Web App → Configuration → Application settings → add the same
   `Cosmos__ConnectionString`.
6. Publish both projects.

## 6. Testing

**Verified live, both sides, in order:**
1. Checked Data Explorer's Items view before running anything - empty,
   as expected (proves the "before" state, not just trusting the "after").
2. Manually triggered `LowStockTimerCheck` via the Portal's Test/Run panel
   (same admin-trigger mechanism as Phase 5) - logs showed
   `Scheduled low-stock check: 149 item(s)...` and `Succeeded`.
3. Checked Data Explorer again - one document now exists,
   `{"id": "latest", "LowStockCount": 149, "CheckedAtUtc": "2026-08-10T05:30:53..."}`.
4. Loaded the live **Reports → Low Stock** page - the new line appeared:
   *"Cached snapshot (Cosmos DB): 149 item(s) as of the Functions app's
   last scheduled check..."* - matching the Cosmos document exactly, and
   the live SQL table below it showing the same items independently.

---
**Previous phase:** [Phase 6 — Service Bus](phase-06-service-bus.md)
**Next phase:** Phase 10 — Application Insights *(not started yet)*
