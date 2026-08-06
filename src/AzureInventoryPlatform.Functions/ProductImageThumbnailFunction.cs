using System.Drawing;
using System.Drawing.Imaging;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AzureInventoryPlatform.Functions;

public class ProductImageThumbnailFunction
{
    private const string ThumbnailContainerName = "product-images-thumbnails";
    private const int MaxDimension = 150;

    private readonly IConfiguration _configuration;
    private readonly ILogger<ProductImageThumbnailFunction> _logger;

    public ProductImageThumbnailFunction(IConfiguration configuration, ILogger<ProductImageThumbnailFunction> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    // Fires automatically whenever a blob is created/overwritten in
    // product-images - no HTTP request, no caller, no token to check.
    // Azure's own storage infrastructure invokes this directly.
    [Function("GenerateProductImageThumbnail")]
    public async Task Run(
        [BlobTrigger("product-images/{name}", Connection = "BlobStorageConnection")] Stream imageStream,
        string name)
    {
        _logger.LogInformation("New product image detected: {Name}", name);

        using var original = new Bitmap(imageStream);
        var (width, height) = Scale(original.Width, original.Height, MaxDimension);
        using var resized = new Bitmap(original, width, height);

        using var outputStream = new MemoryStream();
        resized.Save(outputStream, ImageFormat.Png);
        outputStream.Position = 0;

        var connectionString = _configuration["BlobStorageConnection"]
            ?? throw new InvalidOperationException("Missing BlobStorageConnection configuration.");
        var thumbnailContainer = new BlobContainerClient(connectionString, ThumbnailContainerName);
        await thumbnailContainer.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var blobClient = thumbnailContainer.GetBlobClient(name);
        await blobClient.UploadAsync(outputStream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "image/png" },
        });

        _logger.LogInformation("Thumbnail saved: {Container}/{Name}", ThumbnailContainerName, name);
    }

    private static (int Width, int Height) Scale(int width, int height, int maxDimension)
    {
        if (width <= maxDimension && height <= maxDimension)
        {
            return (width, height);
        }

        var ratio = Math.Min((double)maxDimension / width, (double)maxDimension / height);
        return (Math.Max(1, (int)(width * ratio)), Math.Max(1, (int)(height * ratio)));
    }
}
