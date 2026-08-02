using System.ComponentModel.DataAnnotations;

namespace AzureInventoryPlatform.Web.Models;

public class Warehouse : IEntity
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string WarehouseCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string WarehouseName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string City { get; set; } = string.Empty;
}
