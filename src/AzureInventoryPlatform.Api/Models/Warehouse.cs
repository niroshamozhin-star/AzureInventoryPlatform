using System.ComponentModel.DataAnnotations;

namespace AzureInventoryPlatform.Api.Models;

public class Warehouse : IEntity
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Capacity { get; set; }
}
