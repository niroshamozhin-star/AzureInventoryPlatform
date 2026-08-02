using AzureInventoryPlatform.Web.Models;
using Microsoft.Data.SqlClient;

namespace AzureInventoryPlatform.Web.Data;

public class WarehouseData
{
    private readonly string _connectionString;

    public WarehouseData(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("InventoryDb")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:InventoryDb configuration.");
    }

    public async Task<IReadOnlyList<Warehouse>> GetAllAsync()
    {
        var warehouses = new List<Warehouse>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT WarehouseId, WarehouseCode, WarehouseName, City FROM dbo.Warehouses ORDER BY WarehouseName",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            warehouses.Add(Map(reader));
        }

        return warehouses;
    }

    public async Task<Warehouse?> GetByIdAsync(int id)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT WarehouseId, WarehouseCode, WarehouseName, City FROM dbo.Warehouses WHERE WarehouseId = @Id",
            connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task<Warehouse> AddAsync(Warehouse warehouse)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            """
            INSERT INTO dbo.Warehouses (WarehouseCode, WarehouseName, City)
            OUTPUT INSERTED.WarehouseId
            VALUES (@WarehouseCode, @WarehouseName, @City)
            """, connection);
        AddParameters(command, warehouse);

        warehouse.Id = (int)(await command.ExecuteScalarAsync())!;
        return warehouse;
    }

    public async Task<bool> UpdateAsync(Warehouse warehouse)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "UPDATE dbo.Warehouses SET WarehouseCode = @WarehouseCode, WarehouseName = @WarehouseName, City = @City WHERE WarehouseId = @Id",
            connection);
        AddParameters(command, warehouse);
        command.Parameters.AddWithValue("@Id", warehouse.Id);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("DELETE FROM dbo.Warehouses WHERE WarehouseId = @Id", connection);
        command.Parameters.AddWithValue("@Id", id);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static void AddParameters(SqlCommand command, Warehouse warehouse)
    {
        command.Parameters.AddWithValue("@WarehouseCode", warehouse.WarehouseCode);
        command.Parameters.AddWithValue("@WarehouseName", warehouse.WarehouseName);
        command.Parameters.AddWithValue("@City", warehouse.City);
    }

    private static Warehouse Map(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        WarehouseCode = reader.GetString(1),
        WarehouseName = reader.GetString(2),
        City = reader.GetString(3),
    };
}
