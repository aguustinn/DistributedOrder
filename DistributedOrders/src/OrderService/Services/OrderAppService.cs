using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;
using SharedMessages.Events;
using StackExchange.Redis;
using Order = OrderService.Models.Order;

namespace OrderService.Services;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(CreateOrderRequest request);
    Task<Order?> GetOrderAsync(Guid id);
    Task<List<Order>> GetAllOrdersAsync();
    Task UpdateStatusAsync(Guid orderId, OrderStatus newStatus);
}

public class OrderAppService(
    OrderDbContext db,
    ServiceBusClient busClient,
    IConnectionMultiplexer redis,
    ILogger<OrderAppService> logger) : IOrderService
{
    // Premissa: Transparência de Acesso — cliente não sabe onde os dados estão
    public async Task<Order?> GetOrderAsync(Guid id)
    {
        var cache = redis.GetDatabase();
        var key = $"order:{id}";

        // 1. tenta o cache distribuído (Redis)
        var cached = await cache.StringGetAsync(key);
        if (cached.HasValue)
        {
            logger.LogInformation("Cache HIT para pedido {OrderId}", id);
            return JsonSerializer.Deserialize<Order>(cached!);
        }

        // 2. fallback: banco de dados
        logger.LogInformation("Cache MISS para pedido {OrderId}, buscando no banco", id);
        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);

        if (order is not null)
            await cache.StringSetAsync(key, JsonSerializer.Serialize(order), TimeSpan.FromMinutes(5));

        return order;
    }

    public async Task<List<Order>> GetAllOrdersAsync() =>
        await db.Orders.Include(o => o.Items).OrderByDescending(o => o.CreatedAt).ToListAsync();

    // Premissa: Comunicação assíncrona — publica evento no Service Bus
    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        var order = new Order
        {
            CustomerId   = request.CustomerId,
            CustomerEmail = request.CustomerEmail,
            Items = request.Items.Select(i => new OrderItem
            {
                ProductId   = i.ProductId,
                ProductName = i.ProductName,
                Quantity    = i.Quantity,
                UnitPrice   = i.UnitPrice
            }).ToList()
        };
        order.TotalAmount = order.Items.Sum(i => i.Subtotal);

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Publica evento para o InventoryService verificar o estoque
        var sender = busClient.CreateSender("order-created");
        var payload = new OrderCreatedEvent(
            order.Id,
            order.CustomerId,
            order.CustomerEmail,
            order.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice)).ToList(),
            order.TotalAmount,
            order.CreatedAt
        );

        await sender.SendMessageAsync(new ServiceBusMessage(JsonSerializer.Serialize(payload))
        {
            ContentType    = "application/json",
            MessageId      = order.Id.ToString(),
            CorrelationId  = order.Id.ToString()   // rastreabilidade entre serviços
        });

        logger.LogInformation("Pedido {OrderId} criado e evento publicado no Service Bus", order.Id);
        return order;
    }

    // Chamado pelo Consumer quando InventoryService responde
    public async Task UpdateStatusAsync(Guid orderId, OrderStatus newStatus)
    {
        var order = await db.Orders.FindAsync(orderId)
            ?? throw new KeyNotFoundException($"Pedido {orderId} não encontrado");

        var oldStatus = order.Status.ToString();
        order.Status    = newStatus;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Invalida o cache
        var cache = redis.GetDatabase();
        await cache.KeyDeleteAsync($"order:{orderId}");

        // Publica evento de mudança de status para o NotificationService
        var sender = busClient.CreateSender("order-status-changed");
        var evt = new OrderStatusChangedEvent(orderId, order.CustomerEmail, oldStatus, newStatus.ToString(), DateTime.UtcNow);
        await sender.SendMessageAsync(new ServiceBusMessage(JsonSerializer.Serialize(evt)));

        logger.LogInformation("Pedido {OrderId}: {Old} → {New}", orderId, oldStatus, newStatus);
    }
}

public record CreateOrderRequest(
    Guid CustomerId,
    string CustomerEmail,
    List<OrderItemRequest> Items
);

public record OrderItemRequest(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice
);
