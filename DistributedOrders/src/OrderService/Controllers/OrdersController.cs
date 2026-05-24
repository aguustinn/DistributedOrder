using Microsoft.AspNetCore.Mvc;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    // GET api/orders
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await orderService.GetAllOrdersAsync());

    // GET api/orders/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await orderService.GetOrderAsync(id);
        return order is null ? NotFound() : Ok(order);
    }

    // POST api/orders
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var order = await orderService.CreateOrderAsync(request);
        // 201 Created com Location header apontando para o novo recurso
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }
}
