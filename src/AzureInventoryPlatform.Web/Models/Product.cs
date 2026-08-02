using System.ComponentModel.DataAnnotations;

namespace AzureInventoryPlatform.Web.Models;

public class Product : IEntity
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string ProductCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; }
}
