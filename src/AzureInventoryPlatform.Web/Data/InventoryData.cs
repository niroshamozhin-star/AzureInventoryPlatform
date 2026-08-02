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
            "SELECT InventoryId, ProductId, WarehouseId, Quantity, LastUpdated FROM dbo.Inventory", connection);
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
            "SELECT InventoryId, ProductId, WarehouseId, Quantity, LastUpdated FROM dbo.Inventory WHERE InventoryId = @Id",
            connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task<InventoryItem> AddAsync(InventoryItem item)
    {
        if (item.LastUpdated == default)
        {
            item.LastUpdated = DateTime.UtcNow;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            """
            INSERT INTO dbo.Inventory (ProductId, WarehouseId, Quantity, LastUpdated)
            OUTPUT INSERTED.InventoryId
            VALUES (@ProductId, @WarehouseId, @Quantity, @LastUpdated)
            """, connection);
        AddParameters(command, item);

        item.Id = (int)(await command.ExecuteScalarAsync())!;
        return item;
    }

    public async Task<bool> UpdateAsync(InventoryItem item)
    {
        item.LastUpdated = DateTime.UtcNow;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            """
            UPDATE dbo.Inventory
            SET ProductId = @ProductId, WarehouseId = @WarehouseId, Quantity = @Quantity, LastUpdated = @LastUpdated
            WHERE InventoryId = @Id
            """, connection);
        AddParameters(command, item);
        command.Parameters.AddWithValue("@Id", item.Id);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("DELETE FROM dbo.Inventory WHERE InventoryId = @Id", connection);
        command.Parameters.AddWithValue("@Id", id);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static void AddParameters(SqlCommand command, InventoryItem item)
    {
        command.Parameters.AddWithValue("@ProductId", item.ProductId);
        command.Parameters.AddWithValue("@WarehouseId", item.WarehouseId);
        command.Parameters.AddWithValue("@Quantity", item.Quantity);
        command.Parameters.AddWithValue("@LastUpdated", item.LastUpdated);
    }

    private static InventoryItem Map(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        ProductId = reader.GetInt32(1),
        WarehouseId = reader.GetInt32(2),
        Quantity = reader.GetInt32(3),
        LastUpdated = reader.GetDateTime(4),
    };
}
