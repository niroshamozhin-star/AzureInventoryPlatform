using AzureInventoryPlatform.Api.Models;
using AzureInventoryPlatform.Api.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IRepository<InventoryItem> _inventory;
    private readonly IRepository<Product> _products;
    private readonly IRepository<Warehouse> _warehouses;

    public InventoryController(
        IRepository<InventoryItem> inventory,
        IRepository<Product> products,
        IRepository<Warehouse> warehouses)
    {
        _inventory = inventory;
        _products = products;
        _warehouses = warehouses;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InventoryItem>>> GetAll() =>
        Ok(await _inventory.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InventoryItem>> GetById(int id)
    {
        var item = await _inventory.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<InventoryItem>> Create(InventoryItem item)
    {
        if (await _products.GetByIdAsync(item.ProductId) is null)
        {
            return BadRequest($"Product {item.ProductId} does not exist.");
        }

        if (await _warehouses.GetByIdAsync(item.WarehouseId) is null)
        {
            return BadRequest($"Warehouse {item.WarehouseId} does not exist.");
        }

        var created = await _inventory.AddAsync(item);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPatch("{id:int}/adjust")]
    public async Task<ActionResult<InventoryItem>> AdjustQuantity(int id, [FromBody] int delta)
    {
        var item = await _inventory.GetByIdAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        var newQuantity = item.QuantityOnHand + delta;
        if (newQuantity < 0)
        {
            return BadRequest("Adjustment would result in negative quantity on hand.");
        }

        item.QuantityOnHand = newQuantity;
        await _inventory.UpdateAsync(item);
        return Ok(item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _inventory.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
