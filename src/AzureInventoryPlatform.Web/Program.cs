using AzureInventoryPlatform.Web;
using AzureInventoryPlatform.Web.Models;
using AzureInventoryPlatform.Web.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks();

// In-memory for Phase 1. Phase 2 swaps these for EF Core/Azure SQL-backed
// implementations of the same IRepository<T> contract without touching controllers.
builder.Services.AddSingleton<IRepository<Product>, InMemoryRepository<Product>>();
builder.Services.AddSingleton<IRepository<Warehouse>, InMemoryRepository<Warehouse>>();
builder.Services.AddSingleton<IRepository<InventoryItem>, InMemoryRepository<InventoryItem>>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHealthChecks("/health");

await SeedData.SeedAsync(app.Services);

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
