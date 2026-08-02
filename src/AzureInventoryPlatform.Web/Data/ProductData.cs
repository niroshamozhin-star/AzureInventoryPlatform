using AzureInventoryPlatform.Web.Models;
using Microsoft.Data.SqlClient;

namespace AzureInventoryPlatform.Web.Data;

public class ProductData
{
    private readonly string _connectionString;

    public ProductData(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("InventoryDb")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:InventoryDb configuration.");
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync()
    {
        var products = new List<Product>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT Id, Sku, Name, Description, UnitPrice, Category FROM dbo.Products ORDER BY Name", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            products.Add(Map(reader));
        }

        return products;
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT Id, Sku, Name, Description, UnitPrice, Category FROM dbo.Products WHERE Id = @Id", connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task<Product> AddAsync(Product product)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            """
            INSERT INTO dbo.Products (Sku, Name, Description, UnitPrice, Category)
            OUTPUT INSERTED.Id
            VALUES (@Sku, @Name, @Description, @UnitPrice, @Category)
            """, connection);
        AddParameters(command, product);

        product.Id = (int)(await command.ExecuteScalarAsync())!;
        return product;
    }

    public async Task<bool> UpdateAsync(Product product)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            """
            UPDATE dbo.Products
            SET Sku = @Sku, Name = @Name, Description = @Description, UnitPrice = @UnitPrice, Category = @Category
            WHERE Id = @Id
            """, connection);
        AddParameters(command, product);
        command.Parameters.AddWithValue("@Id", product.Id);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("DELETE FROM dbo.Products WHERE Id = @Id", connection);
        command.Parameters.AddWithValue("@Id", id);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static void AddParameters(SqlCommand command, Product product)
    {
        command.Parameters.AddWithValue("@Sku", product.Sku);
        command.Parameters.AddWithValue("@Name", product.Name);
        command.Parameters.AddWithValue("@Description", (object?)product.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@UnitPrice", product.UnitPrice);
        command.Parameters.AddWithValue("@Category", product.Category);
    }

    private static Product Map(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Sku = reader.GetString(1),
        Name = reader.GetString(2),
        Description = reader.IsDBNull(3) ? null : reader.GetString(3),
        UnitPrice = reader.GetDecimal(4),
        Category = reader.GetString(5),
    };
}
