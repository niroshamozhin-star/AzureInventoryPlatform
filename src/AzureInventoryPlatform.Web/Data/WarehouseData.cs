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
            "SELECT Id, Name, Location, Capacity FROM dbo.Warehouses ORDER BY Name", connection);
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
            "SELECT Id, Name, Location, Capacity FROM dbo.Warehouses WHERE Id = @Id", connection);
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
            INSERT INTO dbo.Warehouses (Name, Location, Capacity)
            OUTPUT INSERTED.Id
            VALUES (@Name, @Location, @Capacity)
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
            "UPDATE dbo.Warehouses SET Name = @Name, Location = @Location, Capacity = @Capacity WHERE Id = @Id",
            connection);
        AddParameters(command, warehouse);
        command.Parameters.AddWithValue("@Id", warehouse.Id);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("DELETE FROM dbo.Warehouses WHERE Id = @Id", connection);
        command.Parameters.AddWithValue("@Id", id);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static void AddParameters(SqlCommand command, Warehouse warehouse)
    {
        command.Parameters.AddWithValue("@Name", warehouse.Name);
        command.Parameters.AddWithValue("@Location", warehouse.Location);
        command.Parameters.AddWithValue("@Capacity", warehouse.Capacity);
    }

    private static Warehouse Map(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        Location = reader.GetString(2),
        Capacity = reader.GetInt32(3),
    };
}
