# Phase 1 — App Service

## AZ-204 objectives covered
- Create Azure App Service Web Apps
- Configure Web Apps (application settings, general settings, health check)
- Deploy code to App Service

## 1. Business scenario

The Inventory Management platform needs a place to run that's always reachable over
HTTPS, doesn't require patching an OS, and can scale later without a rewrite. Before
any data even leaves the process (Azure SQL is Phase 2), the team wants the app
hosted as a managed PaaS service so developers can `git push`/deploy and move on —
no VM, no IIS config, no OS patching.

**App Service** is the natural fit: it's Azure's managed web hosting PaaS for
.NET/Node/Java/etc., with built-in HTTPS, scaling, deployment slots, and health
checks.

## 2. Azure resource to create

Cost: **App Service Plan tier F1 (Free)** — $0/month, shared compute, 60 CPU
minutes/day, no custom domain/SSL, no "Always On" (the app cold-starts after ~20 min
idle — acceptable for a learning project).

### Portal steps
1. Portal search bar → **App Services** → **+ Create** → **Web App**.
2. **Basics** tab:
   - **Subscription**: your subscription.
   - **Resource Group**: **Create new** → `rg-inventory-platform-dev` (keep every
     resource for this project in one resource group so it's easy to tear down later).
   - **Name**: something globally unique, e.g. `inventory-platform-<yourname>` (this
     becomes `https://inventory-platform-<yourname>.azurewebsites.net`).
   - **Publish**: `Code`.
   - **Runtime stack**: `.NET 10 (LTS)`.
   - **Operating System**: `Linux` (cheaper, and matches Phase 11's Docker image later).
   - **Region**: pick one close to you, e.g. `East US`.
3. **App Service Plan** section → **Create new** → name it `plan-inventory-platform-dev`
   → click **Change size**, pick the **Dev/Test** tab → select **F1 Free**.
4. **Monitoring** tab: leave "Enable Application Insights" as **No** for now — we
   wire that up deliberately in Phase 9 so you see exactly what it adds.
5. **Review + create** → **Create**. Takes ~1 minute.

## 3. Code

A single ASP.NET Core 10 MVC app at `src/AzureInventoryPlatform.Web` — no separate
API layer. Each later phase adds its Azure SDK calls directly into this app's
controllers/services.

- **Models**: `Product`, `Warehouse`, `InventoryItem` (all implement `IEntity` for a
  uniform `Id`), plus `WarehouseStockSummary`/`LowStockAlert` for reports.
- **Repositories**: `IRepository<T>` + an `InMemoryRepository<T>` implementation
  (thread-safe `ConcurrentDictionary`). This interface seam is deliberate — Phase 2
  swaps in an EF Core/Azure SQL implementation, Phase 6 swaps in Cosmos DB for
  reports, and controllers never change.
- **Controllers + Views**: `ProductsController`, `WarehousesController` (CRUD),
  `InventoryController` (CRUD + an Adjust-quantity screen), `ReportsController`
  (stock summary + low-stock alerts, joined across the other three repos),
  `HomeController` (a dashboard with live counts). Bootstrap, light theme.
- **`Program.cs`**: registers the repos as singletons (in-memory state must survive
  across requests) and adds `/health` via `AddHealthChecks()`/`MapHealthChecks`.
- **`SeedData.cs`**: seeds 2 products, 2 warehouses, 3 inventory rows on startup so
  the app demos meaningfully without manual setup.
- **Tests**: `tests/AzureInventoryPlatform.Web.Tests` — 7 xUnit tests against a
  `WebApplicationFactory<Program>` (health, seed data rendering, low-stock
  detection, validation).

Run `dotnet test` from the repo root — all 7 pass.

## 4. Azure Portal configuration

After the App Service is created, a few settings matter:

1. **Configuration → General settings**:
   - **Stack settings** should already show `.NET`, version `10 (LTS)`. If it
     doesn't, set it explicitly.
   - **Platform settings → HTTPS Only**: switch to **On** (redirect all HTTP to
     HTTPS — free and App Service handles the cert for the `*.azurewebsites.net`
     domain automatically).
   - **Always On**: greyed out on F1 Free (only available on Basic tier and above).
     This means the app can cold-start after ~20 minutes idle — fine here, but
     worth knowing for production sizing decisions.
2. **Monitoring → Health check**:
   - Enable it, path = `/health` (the endpoint `Program.cs` maps). App Service will
     ping this and route traffic away from unhealthy instances once you scale to
     more than one instance.
3. **Configuration → Application settings**: none needed yet — everything is
   in-memory. Phase 2 is where connection strings show up here.

## 5. Deployment

Two options depending on what's installed — pick whichever you have set up:

### Option A — Visual Studio Code (Azure App Service extension)
1. Install the **Azure App Service** extension in VS Code.
2. Sign in to Azure (`Azure: Sign In` from the command palette).
3. Right-click `src/AzureInventoryPlatform.Web` in the Azure App Service explorer's
   target Web App → **Deploy to Web App**.
4. Confirm the publish-folder prompt (it runs `dotnet publish` for you).

### Option B — Azure CLI (`az webapp deploy`)
Install the CLI first (`winget install Microsoft.AzureCLI`), then:

```bash
az login
az webapp deploy \
  --resource-group rg-inventory-platform-dev \
  --name inventory-platform-<yourname> \
  --src-path ./publish.zip \
  --type zip
```

Package the publish output first:

```bash
dotnet publish src/AzureInventoryPlatform.Web -c Release -o ./publish-output
cd publish-output && zip -r ../publish.zip . && cd ..
```

Both options are **manual, one-off deployments** on purpose — Phase 13 is where we
automate this with a GitHub Actions workflow triggered on every push to `main`.

## 6. Testing

**Locally, before deploying:**
```bash
dotnet run --project src/AzureInventoryPlatform.Web
```
Open `http://localhost:5104` and click through Products, Warehouses, Inventory,
and Reports — or just curl `/health`.

**Against the live App Service, after deploying:**
```bash
curl https://inventory-platform-<yourname>.azurewebsites.net/health
```
Or open `https://inventory-platform-<yourname>.azurewebsites.net` in a browser and
click through the same pages.

**Automated:** `dotnet test` runs the 7 integration tests against an in-process
`WebApplicationFactory` — no Azure resource needed for these to pass, which is the
point of the repository abstraction: business logic is verified independent of
where the app happens to be hosted.

---
**Next phase:** [Phase 2 — Azure SQL](phase-02-azure-sql.md) *(not started yet)*
