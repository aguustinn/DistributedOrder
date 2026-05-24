using System;
using System.Collections.Generic;
namespace SharedMessages.Events
{


// Publicado pelo OrderService quando um pedido é criado
public record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    string CustomerEmail,
    List<OrderItemDto> Items,
    decimal TotalAmount,
    DateTime CreatedAt
);

// Publicado pelo InventoryService após reservar ou recusar o estoque
public record InventoryReservedEvent(
    Guid OrderId,
    bool Success,
    string? FailureReason
);

// Publicado pelo OrderService quando status muda
public record OrderStatusChangedEvent(
    Guid OrderId,
    string CustomerEmail,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAt
);

public record OrderItemDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice
);
}


