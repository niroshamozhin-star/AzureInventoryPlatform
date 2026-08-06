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
            "SELECT ProductId, ProductCode, ProductName, UnitPrice, ReorderLevel, ImageUrl FROM dbo.Products ORDER BY ProductName",
            connection);
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
            "SELECT ProductId, ProductCode, ProductName, UnitPrice, ReorderLevel, ImageUrl FROM dbo.Products WHERE ProductId = @Id",
            connection);
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
            INSERT INTO dbo.Products (ProductCode, ProductName, UnitPrice, ReorderLevel, ImageUrl)
            OUTPUT INSERTED.ProductId
            VALUES (@ProductCode, @ProductName, @UnitPrice, @ReorderLevel, @ImageUrl)
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
            SET ProductCode = @ProductCode, ProductName = @ProductName, UnitPrice = @UnitPrice, ReorderLevel = @ReorderLevel, ImageUrl = @ImageUrl
            WHERE ProductId = @Id
            """, connection);
        AddParameters(command, product);
        command.Parameters.AddWithValue("@Id", product.Id);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> UpdateImageUrlAsync(int id, string imageUrl)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "UPDATE dbo.Products SET ImageUrl = @ImageUrl WHERE ProductId = @Id", connection);
        command.Parameters.AddWithValue("@ImageUrl", imageUrl);
        command.Parameters.AddWithValue("@Id", id);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("DELETE FROM dbo.Products WHERE ProductId = @Id", connection);
        command.Parameters.AddWithValue("@Id", id);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static void AddParameters(SqlCommand command, Product product)
    {
        command.Parameters.AddWithValue("@ProductCode", product.ProductCode);
        command.Parameters.AddWithValue("@ProductName", product.ProductName);
        command.Parameters.AddWithValue("@UnitPrice", product.UnitPrice);
        command.Parameters.AddWithValue("@ReorderLevel", product.ReorderLevel);
        command.Parameters.AddWithValue("@ImageUrl", (object?)product.ImageUrl ?? DBNull.Value);
    }

    private static Product Map(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        ProductCode = reader.GetString(1),
        ProductName = reader.GetString(2),
        UnitPrice = reader.GetDecimal(3),
        ReorderLevel = reader.GetInt32(4),
        ImageUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
    };
}
