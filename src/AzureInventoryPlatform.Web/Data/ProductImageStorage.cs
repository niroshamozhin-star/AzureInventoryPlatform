using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AzureInventoryPlatform.Web.Data;

// Registered as a singleton (not scoped, unlike ProductData/WarehouseData/InventoryData) -
// BlobContainerClient is explicitly documented as safe and intended to be reused across
// requests, unlike a SqlConnection which is opened and disposed per call.
public class ProductImageStorage
{
    private readonly BlobContainerClient _container;

    public ProductImageStorage(IConfiguration configuration)
    {
        var containerName = configuration["Storage:ContainerName"]
            ?? throw new InvalidOperationException("Missing Storage:ContainerName configuration.");

        // Phase 9: if an account name is configured, connect with the Web App's
        // own managed identity instead of an account key - there is no secret
        // to leak here at all. Falls back to a connection string (e.g. local
        // dev against Azurite) when Storage:AccountName isn't set.
        var accountName = configuration["Storage:AccountName"];
        if (!string.IsNullOrWhiteSpace(accountName))
        {
            var containerUri = new Uri($"https://{accountName}.blob.core.windows.net/{containerName}");
            _container = new BlobContainerClient(containerUri, new DefaultAzureCredential());
        }
        else
        {
            var connectionString = configuration["Storage:ConnectionString"]
                ?? throw new InvalidOperationException("Missing Storage:ConnectionString or Storage:AccountName configuration.");
            _container = new BlobContainerClient(connectionString, containerName);
        }
    }

    public async Task<string> UploadAsync(IFormFile file)
    {
        // Tolerates the container having been deleted entirely (not just emptied) -
        // recreates it with the same public-read setup rather than failing.
        await _container.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var blobName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var blobClient = _container.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType },
        });

        return blobClient.Uri.ToString();
    }

    // Deletes a previously uploaded image given the full URL stored on the
    // product - called before replacing a product's image, so re-uploading
    // doesn't leave the old blob behind as an unreferenced orphan.
    public async Task DeleteAsync(string blobUrl)
    {
        var blobName = new Uri(blobUrl).Segments[^1];
        await _container.GetBlobClient(blobName).DeleteIfExistsAsync();
    }
}
