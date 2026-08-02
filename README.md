# Azure Inventory Management & Reporting Platform

A learning project that builds one ASP.NET Core 10 MVC app up incrementally, adding a
single Azure service at a time, to cover the **AZ-204: Developing Solutions for
Microsoft Azure** exam objectives hands-on. The app calls Azure SDKs directly from
its controllers/services as each phase adds a new one — no separate Web API layer.

Business domain: a simple inventory system with four modules — **Products**,
**Warehouses**, **Inventory** (stock levels per product/warehouse), and **Reports**
(stock summaries, low-stock alerts).

Each phase below gets its own write-up in `docs/` covering: business scenario, the
Azure resource created, the code change, Azure Portal configuration steps,
deployment, and how it was tested.

## Progress

| # | Phase | Status | Docs |
|---|-------|--------|------|
| 1 | App Service | ✅ Done | [docs/phase-01-app-service.md](docs/phase-01-app-service.md) |
| 2 | Azure SQL | ⬜ Not started | |
| 3 | Blob Storage | ⬜ Not started | |
| 4 | Azure Functions (HTTP, Blob, Timer) | ⬜ Not started | |
| 5 | Service Bus | ⬜ Not started | |
| 6 | Cosmos DB | ⬜ Not started | |
| 7 | Key Vault | ⬜ Not started | |
| 8 | Managed Identity | ⬜ Not started | |
| 9 | Application Insights | ⬜ Not started | |
| 10 | Azure Monitor | ⬜ Not started | |
| 11 | Docker | ⬜ Not started | |
| 12 | Azure Container Registry | ⬜ Not started | |
| 13 | CI/CD (GitHub Actions) | ⬜ Not started | |

## Solution layout

```
AzureInventoryPlatform.sln
src/
  AzureInventoryPlatform.Web/    ASP.NET Core 10 MVC app — Products/Warehouses/
                                 Inventory/Reports, Bootstrap UI, calls Azure SDKs
                                 directly as each phase adds one
tests/
  AzureInventoryPlatform.Web.Tests/   xUnit + WebApplicationFactory integration tests
docs/
  phase-01-app-service.md, phase-02-azure-sql.md, ...
```

Everything lives in one project on purpose. An earlier version of this repo split
the API and UI into separate projects (`Api` + `Web` + a `Contracts` library just to
share model classes between them) — pure ceremony that didn't serve the AZ-204
learning goal and actually caused a real bug (ASP.NET Core's controller discovery
scans every referenced assembly that touches `Microsoft.AspNetCore.Mvc`, so the Web
project ended up "discovering" the API's own controllers and crashing trying to run
them without the API's DI registrations). One MVC app, calling Azure SDKs directly
from its controllers, sidesteps that entirely and is simpler to reason about.

Inside `AzureInventoryPlatform.Web`:
- `Models/` — `Product`, `Warehouse`, `InventoryItem`, plus `WarehouseStockSummary`/
  `LowStockAlert` for reports.
- `Repositories/` — `IRepository<T>` + an in-memory implementation. Controllers
  depend on the interface, so Phase 2 (Azure SQL/EF Core) and Phase 6 (Cosmos DB,
  for reports) swap in a different implementation without touching controllers or
  views.
- `Controllers/` + `Views/` — standard MVC, Bootstrap-styled, light theme.

## Running locally

```bash
dotnet run --project src/AzureInventoryPlatform.Web
```

Open `http://localhost:5104`.

## Running tests

```bash
dotnet test
```

## Cost philosophy

This is a personal learning/portfolio project — every phase defaults to the
**free or cheapest tier** of each Azure service (App Service F1 Free, Azure SQL
serverless free offer, Cosmos DB free tier, Functions Consumption plan, Service Bus
Basic, etc.). The one paid exception is **Azure Container Registry** (Phase 12,
Basic tier ≈ $0.17/day) — there's no free ACR tier. Each phase's doc calls out the
tier used and its cost.
