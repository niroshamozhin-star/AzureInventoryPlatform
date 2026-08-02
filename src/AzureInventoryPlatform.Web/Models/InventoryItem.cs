using System.ComponentModel.DataAnnotations;

namespace AzureInventoryPlatform.Web.Models;

public class InventoryItem : IEntity
{
    public int Id { get; set; }

    [Required]
    public int ProductId { get; set; }

    [Required]
    public int WarehouseId { get; set; }

    [Range(0, int.MaxValue)]
    public int QuantityOnHand { get; set; }

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; }
}
