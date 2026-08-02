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

## 2. Azure resource created

**Azure SQL Database, S1/serverless-compatible tier**, in the same resource
group as the App Service (`nb-az204-practice`), created via the Azure Portal:
- Server + database with SQL authentication (admin login/password)
- Networking: **Allow Azure services and resources to access this server** =
  Yes (so App Service can reach it), plus the developer's own client IP added
  for direct querying/schema setup

### The schema is the source of truth, not the code
This project's schema was designed first (three tables — Products,
Warehouses, Inventory — with plain, business-friendly column names) and the
tables were created directly against Azure SQL. The application code was then
written to match that schema exactly, rather than the more common order of
"design C# models, generate a schema from them." That matters for how Phase 2
actually got built (see the lesson below).

```sql
CREATE TABLE dbo.Products
(
    ProductId    INT IDENTITY(1,1) PRIMARY KEY,
    ProductCode  NVARCHAR(20)  NOT NULL UNIQUE,
    ProductName  NVARCHAR(200) NOT NULL,
    UnitPrice    DECIMAL(18,2) NOT NULL,
    ReorderLevel INT           NOT NULL
);

CREATE TABLE dbo.Warehouses
(
    WarehouseId   INT IDENTITY(1,1) PRIMARY KEY,
    WarehouseCode NVARCHAR(20)  NOT NULL UNIQUE,
    WarehouseName NVARCHAR(200) NOT NULL,
    City          NVARCHAR(100) NOT NULL
);

CREATE TABLE dbo.Inventory
(
    InventoryId  INT IDENTITY(1,1) PRIMARY KEY,
    ProductId    INT      NOT NULL REFERENCES dbo.Products(ProductId),
    WarehouseId  INT      NOT NULL REFERENCES dbo.Warehouses(WarehouseId),
    Quantity     INT      NOT NULL,
    LastUpdated  DATETIME NOT NULL
);
```
(Full script: `sql/schema.sql`.)

## 3. Code

- **Removed**: `Repositories/IRepository.cs`, `Repositories/InMemoryRepository.cs`.
- **Models** (`Product`, `Warehouse`, `InventoryItem`) renamed field-for-field
  to match the schema above: `ProductCode`/`ProductName`/`ReorderLevel` on
  `Product`; `WarehouseCode`/`WarehouseName`/`City` on `Warehouse`;
  `Quantity`/`LastUpdated` on `InventoryItem` (note `ReorderLevel` lives on the
  *Product*, not the inventory record — one reorder threshold per product,
  shared across every warehouse that stocks it).
- **Added**: `Data/ProductData.cs`, `Data/WarehouseData.cs`, `Data/InventoryData.cs`
  — one plain class per entity, each opening its own `SqlConnection`/`SqlCommand`
  per method (`GetAllAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`,
  `DeleteAsync`), with parameterized queries throughout (no string-concatenated
  SQL, to avoid injection). Registered in `Program.cs` as scoped services —
  concrete classes, not interfaces, since there's nothing to swap them for.
- **`sql/schema.sql`**: kept in the repo as the reference copy of the real
  table structure, for setting up a fresh LocalDB or a new Azure SQL Database
  from scratch.
- **`SeedData.cs`**: checks whether `Products` is empty before inserting demo
  rows (Widget/Gadget, two demo warehouses), so it seeds once and leaves real
  data alone on every subsequent restart. Demo warehouse codes are prefixed
  `SEED-` specifically so they can never collide with imported data (see the
  import section below).
- **Connection string**: read from configuration key `ConnectionStrings:InventoryDb`.
  `appsettings.json` keeps an empty placeholder (safe to commit); the real
  value is supplied via **User Secrets** locally and an **App Service
  Connection String** setting in Azure — never committed.
- **Controllers**: `ProductsController`, `WarehousesController`,
  `InventoryController`, `ReportsController`, `HomeController` depend on
  `ProductData`/`WarehouseData`/`InventoryData` directly instead of an
  `IRepository<T>`.
- **Excel import** (`Controllers/ImportController.cs` + `Views/Import/`): a
  small MVC upload feature — pick Products.xlsx/Warehouses.xlsx/Inventory.xlsx,
  the server reads them with ClosedXML and inserts each row through the same
  `ProductData`/`WarehouseData`/`InventoryData` classes. `ProductCode`/
  `WarehouseCode` from the spreadsheets are mapped to the real database Ids in
  memory for the life of one import request. This is how the 100-product/
  5-warehouse/500-record sample dataset actually got loaded into Azure SQL.

### Lesson learned: a schema mismatch caused a real outage
The app was first built against a placeholder schema invented before the real
Azure SQL Database existed (`Sku`/`Category`/`Description` on Product,
`QuantityOnHand`/`ReorderLevel` on Inventory, table named `InventoryItems`).
Once the real database was created — with its own, different column names —
the deployed app failed immediately on startup with **HTTP 500.30**, because
`SeedData.SeedAsync` runs before the app finishes starting and every query it
ran referenced tables/columns that didn't exist. The fix was to inspect the
live schema directly (`INFORMATION_SCHEMA.COLUMNS`) and rewrite the models and
data-access code to match it exactly, rather than changing the database. Since
the tables were still empty at that point, this was a safe, one-time
realignment rather than a migration.

A second, smaller version of the same lesson: the seed data's demo warehouse
codes (`WH-E`/`WH-W`) happened to collide with the sample spreadsheet's
warehouse codes, causing a `UNIQUE KEY constraint` violation on import. Fixed
by renaming the seed codes to `SEED-E`/`SEED-W` so the two data sources can
never collide.

## 4. Azure Portal configuration

1. **SQL Database → Query editor** (or `sqlcmd`/Azure Data Studio): run
   `sql/schema.sql` to create the three tables before first run.
2. **App Service → Configuration → Connection strings** → **+ New connection
   string**:
   - Name: `InventoryDb`
   - Value: the Azure SQL connection string (from the SQL Database's
     **Connection strings** blade — ADO.NET tab).
   - Type: **SQLAzure**.
   - This surfaces to the app as environment variable `ConnectionStrings__InventoryDb`,
     which `IConfiguration.GetConnectionString("InventoryDb")` reads
     automatically — no code change needed between local and Azure.
3. **SQL Database → Networking**: double check "Allow Azure services" is
   still **On** after any firewall edits, or the App Service can't reach it.

## 5. Deployment

Same two options as Phase 1 (Visual Studio Publish, VS Code Azure extension,
or `az webapp deploy`). The **Connection strings** App Setting above must be
in place *before* the first request hits the app — `SeedData.SeedAsync` runs
on startup and needs the database reachable immediately, or the app fails to
start (HTTP 500.30).

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
instance instead of memory. Use the **Import** page to load the sample
dataset instead of typing 100+ products by hand.

**Against the live App Service, after deploying:** open the site, click
through the same pages, and restart the App Service once to confirm data
survived (the actual point of this phase). Verified end-to-end against the
real Azure SQL Database: 100 products, 5 warehouses, and 500 inventory
records imported via the Import page and confirmed present with a direct
`SELECT COUNT(*)` query.

**Automated:** `dotnet test` runs the same 7 integration tests as Phase 1, via
the same `WebApplicationFactory<Program>`. One real change in testing
philosophy from Phase 1: these tests are no longer fully self-contained — they
need a reachable SQL Server (LocalDB by default) rather than running against
pure in-memory state. That's an inherent trade-off of moving off in-memory
storage, not an accident.

---
**Previous phase:** [Phase 1 — App Service](phase-01-app-service.md)
**Next phase:** [Phase 3 — JWT Authentication](phase-03-jwt-authentication.md) *(not started yet)*
