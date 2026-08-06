using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AzureInventoryPlatform.Functions;

public class LowStockFunction
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LowStockFunction> _logger;

    public LowStockFunction(IConfiguration configuration, ILogger<LowStockFunction> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [Function("GetLowStockReport")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reports/low-stock")] HttpRequest req)
    {
        // AuthorizationLevel.Anonymous here means "skip the Functions runtime's
        // own function-key check" - the JWT check below is this function's
        // real, explicit authorization, done in code rather than framework
        // middleware, so the whole check is visible in one place.
        if (!TryValidateToken(req, out var validationError))
        {
            _logger.LogWarning("Rejected GetLowStockReport call: {Reason}", validationError);
            return new UnauthorizedResult();
        }

        var connectionString = _configuration["SqlConnectionString"]
            ?? throw new InvalidOperationException("Missing SqlConnectionString configuration.");

        var results = new List<object>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            """
            SELECT p.ProductName, w.WarehouseName, i.Quantity, p.ReorderLevel
            FROM dbo.Inventory i
            JOIN dbo.Products p ON p.ProductId = i.ProductId
            JOIN dbo.Warehouses w ON w.WarehouseId = i.WarehouseId
            WHERE i.Quantity <= p.ReorderLevel
            ORDER BY i.Quantity
            """, connection);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new
            {
                ProductName = reader.GetString(0),
                WarehouseName = reader.GetString(1),
                Quantity = reader.GetInt32(2),
                ReorderLevel = reader.GetInt32(3),
            });
        }

        return new OkObjectResult(results);
    }

    private bool TryValidateToken(HttpRequest req, out string error)
    {
        if (!req.Headers.TryGetValue("Authorization", out var authHeader) ||
            !authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            error = "Missing or malformed Authorization header.";
            return false;
        }

        var token = authHeader.ToString()["Bearer ".Length..];
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

        try
        {
            new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
            }, out _);

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
