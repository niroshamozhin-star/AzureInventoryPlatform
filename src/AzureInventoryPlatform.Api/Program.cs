using AzureInventoryPlatform.Api;
using AzureInventoryPlatform.Contracts.Models;
using AzureInventoryPlatform.Api.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// In-memory for Phase 1. Phase 2 swaps these for EF Core/Azure SQL-backed
// implementations of the same IRepository<T> contract without touching controllers.
builder.Services.AddSingleton<IRepository<Product>, InMemoryRepository<Product>>();
builder.Services.AddSingleton<IRepository<Warehouse>, InMemoryRepository<Warehouse>>();
builder.Services.AddSingleton<IRepository<InventoryItem>, InMemoryRepository<InventoryItem>>();

var app = builder.Build();

// Swagger stays on in all environments (including on Azure) so this portfolio
// project is browsable at /swagger without redeploying. Lock this down behind
// auth if this were a real production API.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

await SeedData.SeedAsync(app.Services);

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
