using System.ComponentModel.DataAnnotations;

namespace AzureInventoryPlatform.Contracts.Models;

public class Product : IEntity
{
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string Sku { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;
}
