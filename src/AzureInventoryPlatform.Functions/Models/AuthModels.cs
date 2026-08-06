namespace AzureInventoryPlatform.Functions.Models;

public record TokenRequest(string Username, string Password);

public record TokenResponse(string Token, DateTime ExpiresUtc);
