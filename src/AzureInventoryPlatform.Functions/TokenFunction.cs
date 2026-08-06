using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AzureInventoryPlatform.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AzureInventoryPlatform.Functions;

public class TokenFunction
{
    private readonly IConfiguration _configuration;

    public TokenFunction(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [Function("GetToken")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/token")] HttpRequest req)
    {
        var request = await JsonSerializer.DeserializeAsync<TokenRequest>(
            req.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (request is null || request.Username != _configuration["Auth:Username"] || request.Password != _configuration["Auth:Password"])
        {
            return new UnauthorizedResult();
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresUtc = DateTime.UtcNow.AddHours(1);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: [new Claim(ClaimTypes.Name, request.Username)],
            expires: expiresUtc,
            signingCredentials: credentials);

        return new OkObjectResult(new TokenResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresUtc));
    }
}
