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
        var connectionString = configuration["Storage:ConnectionString"]
            ?? throw new InvalidOperationException("Missing Storage:ConnectionString configuration.");
        var containerName = configuration["Storage:ContainerName"]
            ?? throw new InvalidOperationException("Missing Storage:ContainerName configuration.");

        _container = new BlobContainerClient(connectionString, containerName);
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
