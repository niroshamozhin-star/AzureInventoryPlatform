# Azure Inventory Management & Reporting Platform

An ASP.NET Core 8 MVC application for tracking products, warehouses, and stock
levels — built as a hands-on learning project covering the **AZ-204: Developing
Solutions for Microsoft Azure** exam objectives. Rather than a collection of
disconnected demos, it's one real application that grows a new Azure integration
every phase, so each service is learned in the context of an actual feature it
supports.

## Overview

The app models a small inventory operation: products live in warehouses, stock
levels are tracked per product/warehouse pair, and reports surface stock summaries
and low-stock alerts. It starts simple (Phase 1: in-memory data, hosted on Azure
App Service) and incrementally adopts Azure SQL, Blob Storage, Functions, Service
Bus, Cosmos DB, Key Vault, Managed Identity, Application Insights, Azure Monitor,
Docker, Container Registry, and GitHub Actions CI/CD — one phase, one Azure
service, at a time. See [Progress](#progress) for what's done so far.

## Features

- **Products** — CRUD with SKU/name/category/unit price
- **Warehouses** — CRUD with name/location/capacity
- **Inventory** — stock records linking a product to a warehouse, with a
  dedicated quantity-adjustment screen (`+`/`-` deltas rather than blind edits)
- **Reports** — stock value summary per warehouse, and a low-stock alert list
  (items at or below their reorder level), with the same rows visually flagged
  wherever they appear (Inventory list and Reports page)
- **Dashboard** — live counts across all four modules on the landing page

## Azure services used

| Service | Status | Purpose |
|---|---|---|
| Azure App Service | ✅ Phase 1 | Hosts the MVC app (Linux, F1 Free tier) |
| Azure SQL | ⬜ Phase 2 | Relational persistence via EF Core, replacing the in-memory store |
| Blob Storage | ⬜ Phase 3 | Product images, exported report files |
| Azure Functions | ⬜ Phase 4 | HTTP, Blob-triggered, and Timer-triggered background work |
| Service Bus | ⬜ Phase 5 | Async inventory-changed events |
| Cosmos DB | ⬜ Phase 6 | Denormalized store for the Reports module |
| Key Vault | ⬜ Phase 7 | Secrets/connection strings out of config files |
| Managed Identity | ⬜ Phase 8 | Passwordless auth from App Service to the above |
| Application Insights | ⬜ Phase 9 | Telemetry, custom events/metrics |
| Azure Monitor | ⬜ Phase 10 | Alert rules and dashboards |
| Docker | ⬜ Phase 11 | Containerize the app |
| Azure Container Registry | ⬜ Phase 12 | Host the container image |
| GitHub Actions | ⬜ Phase 13 | CI/CD straight from this repo |

## Architecture

![Architecture diagram](docs/architecture/architecture.png)

A single ASP.NET Core MVC app — no separate Web API layer. Controllers call
Azure SDKs (or, for now, an in-memory store) directly through an `IRepository<T>`
abstraction, so later phases can swap the storage backend without touching
controllers or views:

```
src/AzureInventoryPlatform.Web/
  Models/        Product, Warehouse, InventoryItem, report DTOs
  Repositories/  IRepository<T> + current implementation
  Controllers/   Products, Warehouses, Inventory, Reports, Home
  Views/         Razor + Bootstrap, light theme
```

This intentionally stays as one project. An earlier version of this repo split
the API and UI into separate projects joined by a shared `Contracts` library —
pure ceremony for a learning project, and it caused a real bug (ASP.NET Core's
controller discovery scans every referenced assembly that touches
`Microsoft.AspNetCore.Mvc`, so the UI project ended up "discovering" and trying
to run the API's own controllers without the API's DI registrations wired up).
One project, calling Azure SDKs directly, sidesteps that entirely.

## Screenshots

| Dashboard | Products |
|---|---|
| ![Dashboard](docs/screenshots/dashboard.png) | ![Products](docs/screenshots/products.png) |

| Warehouses | Inventory |
|---|---|
| ![Warehouses](docs/screenshots/warehouses.png) | ![Inventory](docs/screenshots/inventory.png) |

| Reports |
|---|
| ![Reports](docs/screenshots/reports.png) |

## How to run locally

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
git clone https://github.com/niroshamozhin-star/AzureInventoryPlatform.git
cd AzureInventoryPlatform
dotnet run --project src/AzureInventoryPlatform.Web
```

Open `http://localhost:5104`. The app seeds itself with sample products,
warehouses, and inventory records on startup — no database or Azure resource
required for Phase 1.

Run the test suite with:

```bash
dotnet test
```

## Future enhancements

Tracked as the remaining AZ-204 phases (see the [Azure services table](#azure-services-used)
above), plus:
- Authentication/authorization (Azure AD / Entra ID) once a real user model exists
- Pagination and search on the Products/Inventory list pages
- Exporting reports to Blob Storage as CSV/PDF (natural fit once Phase 3 lands)
- A CI pipeline that runs `dotnet test` on every PR, ahead of the full CD story in Phase 13

## Progress

| # | Phase | Status | Docs |
|---|-------|--------|------|
| 1 | App Service | ✅ Done | [docs/learning-notes/phase-01-app-service.md](docs/learning-notes/phase-01-app-service.md) |
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

Each phase gets its own write-up in `docs/learning-notes/` covering: business scenario, the
Azure resource created, the code change, Azure Portal configuration steps,
deployment, and how it was tested.

## Security

No secrets, connection strings, or credentials are committed to this repository.
`.gitignore` excludes environment-specific config (`appsettings.Development.json`),
Visual Studio publish profiles (`*.pubxml`, `Properties/ServiceDependencies/`),
and common secret file patterns. As later phases introduce real Azure connection
strings, they'll be sourced from
[User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) or
environment variables locally, and **Managed Identity** (Phase 8) in Azure —
never hardcoded in source.

## Cost philosophy

This is a personal learning/portfolio project — every phase defaults to the
**free or cheapest tier** of each Azure service (App Service F1 Free, Azure SQL
serverless free offer, Cosmos DB free tier, Functions Consumption plan, Service Bus
Basic, etc.). The one paid exception is **Azure Container Registry** (Phase 12,
Basic tier ≈ $0.17/day) — there's no free ACR tier. Each phase's doc calls out the
tier used and its cost.

## License

[MIT](LICENSE)
