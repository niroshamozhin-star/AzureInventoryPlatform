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

Both the Web App and the Function App send their telemetry to the same
Application Insights resource (`niro-inventory-functions`), rather than
one each - the standard real-world pattern for a multi-component app, so
requests across both apps show up correlated in one place instead of
split across two dashboards.

## 2. Flow

The two apps don't call each other directly for telemetry - each one
independently sends its own data to the same connection string. The code
on each side is a mirror image of the other:

```
WEB APP                                          FUNCTION APP
────────                                         ────────────
Program.cs                                        Program.cs
  builder.Services                                  builder.ConfigureFunctionsWebApplication()
    .AddApplicationInsightsTelemetry()                .Services
         |                                              .AddApplicationInsightsTelemetryWorkerService()
         | reads config key                             .ConfigureFunctionsApplicationInsights()
         v                                                     |
  APPLICATIONINSIGHTS_CONNECTION_STRING            reads config key
  (app setting on Web App)                                     v
         |                                          APPLICATIONINSIGHTS_CONNECTION_STRING
         |                                          (app setting on Function App)
         |                                                     |
         |         SAME connection string value on both  <-----+
         |
         v                                                     v
  Every controller action auto-tracked:            Every function invocation auto-tracked:
  - HTTP request                                    - Trigger firing
  - SQL/Blob/Cosmos calls                            - SQL/Blob/Cosmos calls
  - Exceptions                                       - Exceptions
  PLUS deliberate:
  _telemetry.TrackEvent("ProductCreated", ...)
  in ProductsController.Create()
         |                                                     |
         +----------------------+------------------------------+
                                v
              Application Insights resource
              "niro-inventory-functions" (ONE resource, shared)

              Live Metrics -> "2 servers online"
              Transaction search -> both apps' requests, correlated
              by Operation ID whenever they're part of the same
              real request chain (e.g. a Service Bus message
              published by the Web App and consumed by the Function).
```

## 3. Azure resource

**Application Insights** `niro-inventory-functions`, shared by both apps
rather than one resource each. Backed by a Log Analytics workspace
(`DefaultWorkspace-...-CID`).

## 4. Code

- **Functions project** - added `Microsoft.Azure.Functions.Worker.ApplicationInsights`
  and `Microsoft.ApplicationInsights.WorkerService` packages. In `Program.cs`,
  after `ConfigureFunctionsWebApplication()`:
  ```csharp
  builder.Services
      .AddApplicationInsightsTelemetryWorkerService()
      .ConfigureFunctionsApplicationInsights();
  ```
  This is what makes the isolated worker model actually send telemetry -
  it reads the connection string from config and starts forwarding every
  function invocation, dependency call, and exception automatically.
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

1. Copied the **Connection String** from the `niro-inventory-functions`
   Application Insights resource.
2. Web App → Configuration → Application settings → added
   `APPLICATIONINSIGHTS_CONNECTION_STRING` with that value.
3. Function App → Configuration → confirmed the same setting is present
   there too, pointing at the same connection string.
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
**Next phase:** [Phase 11 — Azure Monitor](phase-11-azure-monitor.md)
