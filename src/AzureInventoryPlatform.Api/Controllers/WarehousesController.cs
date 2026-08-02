using AzureInventoryPlatform.Api.Models;
using AzureInventoryPlatform.Api.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AzureInventoryPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WarehousesController : ControllerBase
{
    private readonly IRepository<Warehouse> _warehouses;

    public WarehousesController(IRepository<Warehouse> warehouses)
    {
        _warehouses = warehouses;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Warehouse>>> GetAll() =>
        Ok(await _warehouses.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Warehouse>> GetById(int id)
    {
        var warehouse = await _warehouses.GetByIdAsync(id);
        return warehouse is null ? NotFound() : Ok(warehouse);
    }

    [HttpPost]
    public async Task<ActionResult<Warehouse>> Create(Warehouse warehouse)
    {
        var created = await _warehouses.AddAsync(warehouse);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Warehouse warehouse)
    {
        if (id != warehouse.Id)
        {
            return BadRequest("Route id does not match body id.");
        }

        var updated = await _warehouses.UpdateAsync(warehouse);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _warehouses.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
