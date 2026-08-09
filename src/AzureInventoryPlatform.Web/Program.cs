using Azure.Identity;
using AzureInventoryPlatform.Web;
using AzureInventoryPlatform.Web.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Phase 8/9: Key Vault + Managed Identity. If KeyVault:Uri is set (only on
// Azure - locally this stays blank and config falls back to User Secrets),
// pull secrets from Key Vault using the Web App's own system-assigned
// managed identity. No client secret or key is ever stored anywhere - Azure
// AD handles the authentication behind DefaultAzureCredential.
var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}

builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks();

// Phase 2: direct ADO.NET access to Azure SQL, no repository abstraction.
builder.Services.AddScoped<ProductData>();
builder.Services.AddScoped<WarehouseData>();
builder.Services.AddScoped<InventoryData>();

// Phase 4: Blob Storage for product images. Singleton, not scoped - the
// Azure SDK's BlobContainerClient is documented as safe to reuse across
// requests, unlike a SqlConnection which is opened/closed per call.
builder.Services.AddSingleton<ProductImageStorage>();

// Phase 6: Service Bus - publishes an event whenever inventory is adjusted.
builder.Services.AddSingleton<InventoryEventPublisher>();

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
