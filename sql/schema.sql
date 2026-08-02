-- Azure Inventory Platform - Phase 2 schema
-- Run this once against your target database (LocalDB for local dev/tests,
-- Azure SQL Database in the cloud) before starting the app.

IF OBJECT_ID('dbo.Inventory', 'U') IS NULL
BEGIN
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
END
