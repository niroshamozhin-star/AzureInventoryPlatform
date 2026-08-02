# Phase 2 — Azure SQL

## AZ-204 objectives covered
- Create and configure Azure SQL Database
- Connect an app to Azure SQL and manage connection strings/secrets
- Implement data access with ADO.NET

## 1. Business scenario

In-memory data (Phase 1) resets every time the app restarts — fine for a demo,
useless for a real inventory system where products, warehouses, and stock
counts need to survive restarts, deployments, and scaling to more than one
instance. Azure SQL gives the app a real, durable, relational store without
having to run or patch a SQL Server VM.

**No repository abstraction this phase.** Phase 1 deliberately went through an
`IRepository<T>` interface so the storage backend could be swapped later. That
swap is happening now — but instead of adding an EF Core implementation behind
the interface, the interface itself is removed. Controllers call plain
ADO.NET (`Microsoft.Data.SqlClient`) data-access classes directly. Simpler to
read for a learning project, and it matches the stack this project is
deliberately built around: **MVC, Controllers, Models, Views, ADO.NET, Azure
SQL** — no ORM, no abstraction layer standing between the controller and the
database.

## 2. Azure resource to create

Cost: **Azure SQL Database, Serverless tier** — auto-pauses after a period of
inactivity, so it costs close to nothing for a learning project (compute
resumes automatically on the next connection, with a short cold-start delay).

### Portal steps
1. Portal search bar → **SQL databases** → **+ Create**.
2. **Basics** tab:
   - **Resource group**: reuse `nb-az204-practice` (same group as the App Service).
   - **Database name**: `AzureInventoryPlatform`.
   - **Server**: **Create new** →
     - Server name: something globally unique, e.g. `niro-inventory-sql`.
     - Location: same region as the App Service (`Central India`), to avoid
       cross-region latency/egress.
     - Authentication: **SQL authentication** → set an admin login/password
       (Microsoft Entra-only auth is the more "correct" long-term answer, and
       is exactly what Phase 9 — Managed Identity — replaces this with).
   - **Compute + storage** → **Configure database** → **Serverless** tier,
     minimum vCores as low as it'll allow → **Apply**.
3. **Networking** tab:
   - **Connectivity method**: Public endpoint.
   - **Allow Azure services and resources to access this server**: **Yes**
     (needed so the App Service can reach it).
   - **Add current client IP address**: **Yes** (needed so you can run the
     schema script and query it from your own machine).
4. **Review + create** → **Create**. Takes a few minutes.

## 3. Code

- **Removed**: `Repositories/IRepository.cs`, `Repositories/InMemoryRepository.cs`.
- **Added**: `Data/ProductData.cs`, `Data/WarehouseData.cs`, `Data/InventoryData.cs`
  — one plain class per entity, each opening its own `SqlConnection`/`SqlCommand`
  per method (`GetAllAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`,
  `DeleteAsync`), with parameterized queries throughout (no string-concatenated
  SQL, to avoid injection). Registered in `Program.cs` as scoped services —
  concrete classes, not interfaces, since there's nothing to swap them for.
- **`sql/schema.sql`**: `CREATE TABLE` script for `Products`, `Warehouses`,
  `InventoryItems`, matching the existing `Product`/`Warehouse`/`InventoryItem`
  models. Run once against whichever database you're pointing at.
- **`SeedData.cs`**: now checks whether `Products` is empty before inserting
  the sample rows, so it seeds once and leaves real data alone on every
  subsequent restart (the in-memory version could always start from zero;
  a real database can't).
- **Connection string**: read from configuration key `ConnectionStrings:InventoryDb`.
  `appsettings.json` keeps an empty placeholder (safe to commit); the real
  value is supplied via **User Secrets** locally and an **App Service
  Connection String** setting in Azure — never committed.
- **Controllers**: `ProductsController`, `WarehousesController`,
  `InventoryController`, `ReportsController`, `HomeController` now depend on
  `ProductData`/`WarehouseData`/`InventoryData` directly instead of
  `IRepository<T>`. Logic is otherwise unchanged.

## 4. Azure Portal configuration

1. **SQL Database → Query editor**: run `sql/schema.sql` here (or via
   `sqlcmd`/Azure Data Studio) to create the three tables before first run.
2. **App Service → Configuration → Connection strings** → **+ New connection
   string**:
   - Name: `InventoryDb`
   - Value: the Azure SQL connection string (from the SQL Database's
     **Connection strings** blade — ADO.NET tab).
   - Type: **SQLAzure**.
   - This surfaces to the app as `ConnectionStrings__InventoryDb`, which
     `IConfiguration.GetConnectionString("InventoryDb")` reads automatically —
     no code change needed between local and Azure.
3. **SQL Database → Networking**: double check "Allow Azure services" is
   still **On** after any firewall edits, or the App Service can't reach it.

## 5. Deployment

Same two options as Phase 1 (VS Code Azure extension, or `az webapp deploy`).
The only addition: make sure the **Connection strings** App Setting above is
in place *before* the first request hits a controller — `SeedData.SeedAsync`
runs on startup and needs the database reachable immediately.

## 6. Testing

**Locally, before deploying:**
```bash
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "CREATE DATABASE AzureInventoryPlatform"
sqlcmd -S "(localdb)\MSSQLLocalDB" -d AzureInventoryPlatform -i sql/schema.sql
cd src/AzureInventoryPlatform.Web
dotnet user-secrets set "ConnectionStrings:InventoryDb" "Server=(localdb)\MSSQLLocalDB;Database=AzureInventoryPlatform;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet run
```
Open `http://localhost:5104` and click through Products, Warehouses, Inventory,
and Reports — same pages as Phase 1, now reading from a real SQL Server
instance instead of memory.

**Against the live App Service, after deploying:** open the site and click
through the same pages; also worth restarting the App Service once and
confirming data survived (the actual point of this phase).

**Automated:** `dotnet test` runs the same 7 integration tests as Phase 1, via
the same `WebApplicationFactory<Program>`. One real change in testing
philosophy from Phase 1: these tests are no longer fully self-contained — they
need a reachable SQL Server (LocalDB by default, configured the same way as
local dev above) rather than running against pure in-memory state. That's an
inherent trade-off of moving off in-memory storage, not an accident.

---
**Previous phase:** [Phase 1 — App Service](phase-01-app-service.md)
**Next phase:** [Phase 3 — JWT Authentication](phase-03-jwt-authentication.md) *(not started yet)*
