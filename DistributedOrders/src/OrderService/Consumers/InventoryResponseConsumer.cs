using System.Text.Json;
using Azure.Messaging.ServiceBus;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Consumers;

// Premissa: Tolerância a Falhas — BackgroundService com retry automático
public class InventoryResponseConsumer(
    ServiceBusClient busClient,
    IServiceScopeFactory scopeFactory,
    ILogger<InventoryResponseConsumer> logger) : BackgroundService
{
    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Premissa: Replicação — o Service Bus garante entrega mesmo se o serviço caiu
        _processor = busClient.CreateProcessor("inventory-reserved", new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 5,         // processamento paralelo
            AutoCompleteMessages = false,   // acknowledges manualmente (garante exactly-once)
            MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
        });

        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync   += HandleErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);
        logger.LogInformation("InventoryResponseConsumer iniciado — ouvindo 'inventory-reserved'");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var body = args.Message.Body.ToString();
            var evt  = JsonSerializer.Deserialize<SharedMessages.Events.InventoryReservedEvent>(body)!;

            using var scope   = scopeFactory.CreateScope();
            var orderService  = scope.ServiceProvider.GetRequiredService<IOrderService>();

            // Define novo status com base na resposta do InventoryService
            var newStatus = evt.Success ? OrderStatus.InventoryReserved : OrderStatus.Rejected;
            await orderService.UpdateStatusAsync(evt.OrderId, newStatus);

            await args.CompleteMessageAsync(args.Message);   // ACK — remove da fila
            logger.LogInformation("Mensagem processada: Pedido {OrderId} → {Status}", evt.OrderId, newStatus);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao processar mensagem. Mensagem será reenviada para dead-letter após 3 tentativas.");
            await args.AbandonMessageAsync(args.Message);    // NACK — Service Bus fará retry
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Erro no processador do Service Bus: {Source}", args.ErrorSource);
        return Task.CompletedTask;
    }

    public override async ValueTask DisposeAsync()
    {
        if (_processor is not null) await _processor.DisposeAsync();
        await base.DisposeAsync();
    }
}
