-- Azure Inventory Platform — Phase 2 schema
-- Run this once against your target database (LocalDB for local dev/tests,
-- Azure SQL Database in the cloud) before starting the app.

IF OBJECT_ID('dbo.InventoryItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        Sku         NVARCHAR(30)  NOT NULL,
        Name        NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        UnitPrice   DECIMAL(18,2) NOT NULL,
        Category    NVARCHAR(100) NOT NULL
    );

    CREATE TABLE dbo.Warehouses
    (
        Id       INT IDENTITY(1,1) PRIMARY KEY,
        Name     NVARCHAR(200) NOT NULL,
        Location NVARCHAR(200) NOT NULL,
        Capacity INT           NOT NULL
    );

    CREATE TABLE dbo.InventoryItems
    (
        Id             INT IDENTITY(1,1) PRIMARY KEY,
        ProductId      INT NOT NULL REFERENCES dbo.Products(Id),
        WarehouseId    INT NOT NULL REFERENCES dbo.Warehouses(Id),
        QuantityOnHand INT NOT NULL,
        ReorderLevel   INT NOT NULL
    );
END
