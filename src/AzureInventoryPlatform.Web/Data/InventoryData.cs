using AzureInventoryPlatform.Web.Models;
using Microsoft.Data.SqlClient;

namespace AzureInventoryPlatform.Web.Data;

public class InventoryData
{
    private readonly string _connectionString;

    public InventoryData(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("InventoryDb")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:InventoryDb configuration.");
    }

    public async Task<IReadOnlyList<InventoryItem>> GetAllAsync()
    {
        var items = new List<InventoryItem>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT Id, ProductId, WarehouseId, QuantityOnHand, ReorderLevel FROM dbo.InventoryItems", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public async Task<InventoryItem?> GetByIdAsync(int id)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT Id, ProductId, WarehouseId, QuantityOnHand, ReorderLevel FROM dbo.InventoryItems WHERE Id = @Id",
            connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task<InventoryItem> AddAsync(InventoryItem item)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            """
            INSERT INTO dbo.InventoryItems (ProductId, WarehouseId, QuantityOnHand, ReorderLevel)
            OUTPUT INSERTED.Id
            VALUES (@ProductId, @WarehouseId, @QuantityOnHand, @ReorderLevel)
            """, connection);
        AddParameters(command, item);

        item.Id = (int)(await command.ExecuteScalarAsync())!;
        return item;
    }

    public async Task<bool> UpdateAsync(InventoryItem item)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            """
            UPDATE dbo.InventoryItems
            SET ProductId = @ProductId, WarehouseId = @WarehouseId,
                QuantityOnHand = @QuantityOnHand, ReorderLevel = @ReorderLevel
            WHERE Id = @Id
            """, connection);
        AddParameters(command, item);
        command.Parameters.AddWithValue("@Id", item.Id);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("DELETE FROM dbo.InventoryItems WHERE Id = @Id", connection);
        command.Parameters.AddWithValue("@Id", id);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static void AddParameters(SqlCommand command, InventoryItem item)
    {
        command.Parameters.AddWithValue("@ProductId", item.ProductId);
        command.Parameters.AddWithValue("@WarehouseId", item.WarehouseId);
        command.Parameters.AddWithValue("@QuantityOnHand", item.QuantityOnHand);
        command.Parameters.AddWithValue("@ReorderLevel", item.ReorderLevel);
    }

    private static InventoryItem Map(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        ProductId = reader.GetInt32(1),
        WarehouseId = reader.GetInt32(2),
        QuantityOnHand = reader.GetInt32(3),
        ReorderLevel = reader.GetInt32(4),
    };
}
