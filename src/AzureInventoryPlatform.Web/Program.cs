using AzureInventoryPlatform.Web;
using AzureInventoryPlatform.Web.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks();

// Phase 2: direct ADO.NET access to Azure SQL, no repository abstraction.
builder.Services.AddScoped<ProductData>();
builder.Services.AddScoped<WarehouseData>();
builder.Services.AddScoped<InventoryData>();

// Phase 3: cookie-based login for the whole app. Every controller is
// [Authorize] by default except AccountController (Login/Logout), which is
// [AllowAnonymous] - unauthenticated requests get redirected to LoginPath.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHealthChecks("/health");

await SeedData.SeedAsync(app.Services);

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
