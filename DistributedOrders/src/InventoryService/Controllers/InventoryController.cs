using InventoryService.Services;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController(IInventoryService inventoryService) : ControllerBase
{
    // GET api/inventory
    [HttpGet]
    public async Task<IActionResult> GetProducts() =>
        Ok(await inventoryService.GetProductsAsync());
}
