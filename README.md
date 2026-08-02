# Azure Inventory Management & Reporting Platform

A learning project that builds one ASP.NET Core 8 Web API up incrementally, adding a
single Azure service at a time, to cover the **AZ-204: Developing Solutions for
Microsoft Azure** exam objectives hands-on.

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
  AzureInventoryPlatform.Api/     ASP.NET Core 8 Web API (Product/Warehouse/Inventory/Reports)
  AzureInventoryPlatform.Web/     ASP.NET Core 8 MVC UI, consumes the Web API over HTTP
tests/
  AzureInventoryPlatform.Api.Tests/   xUnit + WebApplicationFactory integration tests
docs/
  phase-01-app-service.md, phase-02-azure-sql.md, ...
```

The MVC UI (`AzureInventoryPlatform.Web`) isn't part of the AZ-204 phase sequence —
it's a simple Bootstrap front end (dashboard + CRUD for Products/Warehouses/Inventory,
a Reports page) that talks to the API via a typed `HttpClient` per module
(`ApiClients/`), so there's something to click through instead of only curling
JSON. Its `ApiBaseUrl` setting (`appsettings.json`) points at the API — update it
once the API has a real Azure URL instead of localhost.

## Running locally

Run the API first, then the Web UI in a second terminal (the UI's `ApiBaseUrl`
defaults to `http://localhost:5080`):

```bash
dotnet run --project src/AzureInventoryPlatform.Api   # http://localhost:5080/swagger
dotnet run --project src/AzureInventoryPlatform.Web    # http://localhost:5104
```

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
