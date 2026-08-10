# Phase 10 — Application Insights

## AZ-204 objectives covered
- Implement Application Insights (custom telemetry, live metrics, distributed tracing)

## 1. Business scenario

Up to now, the only way to see what's happening on the live site is
watching Log Stream in real time or reading whatever a specific Function
happened to log. Application Insights gives every request, every
dependency call (SQL, Blob, Cosmos), and every exception a searchable,
timestamped record automatically - plus a place to add deliberate
business telemetry ("a product was created") on top of that automatic
layer.

**A wrinkle worth naming:** the Function App already had an Application
Insights *resource* (`niro-inventory-functions`) from when it was first
created - but the isolated-worker code was never actually wired to send
telemetry to it. Having the resource and the connection string setting
present isn't enough on its own for the isolated worker model; it needs
an explicit registration call. This phase fixes that gap and also
connects the Web App to the **same** resource, rather than creating a
second one - the standard real-world pattern for a multi-component app,
so requests across both apps show up correlated in one place instead of
split across two dashboards.

## 2. Flow

```
Automatic instrumentation (no code beyond one registration call):
  every HTTP request, every outgoing SQL/Blob/Cosmos call, every
  exception -> captured and sent to Application Insights automatically,
  correlated under one "Operation ID" per request.

Deliberate custom telemetry (Phase 10's own code):
  ProductsController.Create() succeeds
      -> _telemetry.TrackEvent("ProductCreated", { ProductCode, HasImage })
      -> shows up as its own event, nested inside that same request's
         end-to-end transaction trace alongside the automatic entries.

Both apps -> same Application Insights resource (niro-inventory-functions)
      -> Live Metrics shows "2 servers online"
      -> Transaction search / Logs shows traffic from both, correlated.
```

## 3. Azure resource

**Application Insights** `niro-inventory-functions` - already existed
(auto-created alongside the Function App), reused rather than duplicated
for the Web App. Backed by a Log Analytics workspace
(`DefaultWorkspace-...-CID`, also auto-created).

## 4. Code

- **Functions project** - added `Microsoft.Azure.Functions.Worker.ApplicationInsights`
  and `Microsoft.ApplicationInsights.WorkerService` packages. In `Program.cs`,
  after `ConfigureFunctionsWebApplication()`:
  ```csharp
  builder.Services
      .AddApplicationInsightsTelemetryWorkerService()
      .ConfigureFunctionsApplicationInsights();
  ```
  This is the actual fix for the "resource exists but nothing's being
  sent" gap - the isolated worker model needs this explicit call; the
  older in-process model didn't.
- **Web project** - added `Microsoft.ApplicationInsights.AspNetCore`. In
  `Program.cs`: `builder.Services.AddApplicationInsightsTelemetry();` -
  one line, reads the connection string from config automatically, gives
  every controller action automatic request/dependency/exception tracking
  for free.
- **`ProductsController.Create()`** - after a product is successfully
  saved, injects `TelemetryClient` and calls:
  ```csharp
  _telemetry.TrackEvent("ProductCreated", new Dictionary<string, string>
  {
      ["ProductCode"] = product.ProductCode,
      ["HasImage"] = (imageFile is { Length: > 0 }).ToString(),
  });
  ```
  Deliberate business telemetry, distinct from the automatic layer - "a
  product was created" is a fact about the domain, not just "a request
  happened."

## 5. Azure Portal configuration

1. Copied the **Connection String** from the existing
   `niro-inventory-functions` Application Insights resource.
2. Web App → Configuration → Application settings → added
   `APPLICATIONINSIGHTS_CONNECTION_STRING` with that value.
3. Confirmed the Function App already had the same setting (it did, from
   creation-time auto-provisioning) - just needed the code fix above to
   actually use it.
4. Published both projects.

## 6. Testing

**Verified live, both layers:**
- **Automatic instrumentation:** opened Live Metrics, browsed the site
  (Dashboard, Products, Inventory) - "2 servers online" confirmed both
  apps reporting, and the Incoming/Outgoing Requests graphs visibly
  spiked in real time during the interaction.
- **Custom telemetry:** created a real product with an image on the live
  site, waited ~1-2 minutes for ingestion, then found the `ProductCreated`
  event via Transaction search - confirmed `ProductCode` and `HasImage`
  matched what was actually submitted.
- **Distributed tracing, as a bonus:** the same transaction's end-to-end
  view showed the *entire* dependency chain for that one request in
  order - `BlobContainerClient.CreateIfNotExists`, a `GET /msi/token/`
  call (the managed identity token acquisition from Phase 9, now visible
  in a trace instead of just inferred), the blob `PUT`, and the SQL
  dependency call - all correlated under one Operation ID, all captured
  with zero extra code beyond the one registration line.

---
**Previous phase:** [Phase 7 — Cosmos DB](phase-07-cosmos-db.md)
**Next phase:** Phase 11 — Azure Monitor *(not started yet)*
