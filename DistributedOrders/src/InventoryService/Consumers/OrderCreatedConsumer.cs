using System.Text.Json;
using Azure.Messaging.ServiceBus;
using InventoryService.Services;
using SharedMessages.Events;

namespace InventoryService.Consumers;

// Premissa: Comunicação Assíncrona e Tolerância a Falhas
public class OrderCreatedConsumer(
    ServiceBusClient busClient,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = busClient.CreateProcessor("order-created", new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls  = 10,
            AutoCompleteMessages = false
        });

        _processor.ProcessMessageAsync += HandleAsync;
        _processor.ProcessErrorAsync   += OnErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);
        logger.LogInformation("OrderCreatedConsumer iniciado — ouvindo 'order-created'");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleAsync(ProcessMessageEventArgs args)
    {
        OrderCreatedEvent? evt = null;
        try
        {
            evt = JsonSerializer.Deserialize<OrderCreatedEvent>(args.Message.Body.ToString())!;

            using var scope      = scopeFactory.CreateScope();
            var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

            var itemsToReserve = evt.Items
                .Select(i => (i.ProductId, i.Quantity))
                .ToList();

            var (success, reason) = await inventoryService.TryReserveStockAsync(evt.OrderId, itemsToReserve);

            // Publica resposta de volta ao OrderService
            var replyEvent = new InventoryReservedEvent(evt.OrderId, success, reason);
            var sender     = busClient.CreateSender("inventory-reserved");
            await sender.SendMessageAsync(new ServiceBusMessage(JsonSerializer.Serialize(replyEvent))
            {
                CorrelationId = evt.OrderId.ToString()
            });

            await args.CompleteMessageAsync(args.Message);
            logger.LogInformation("Pedido {OrderId}: reserva={Success}", evt.OrderId, success);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao processar OrderCreatedEvent para pedido {OrderId}", evt?.OrderId);
            await args.AbandonMessageAsync(args.Message);
        }
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Erro no consumer: {Source}", args.ErrorSource);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor is not null) await _processor.DisposeAsync();
        base.Dispose();
    }
}
