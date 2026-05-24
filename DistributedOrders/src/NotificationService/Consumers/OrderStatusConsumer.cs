using System.Text.Json;
using Azure.Messaging.ServiceBus;
using NotificationService.Services;
using SharedMessages.Events;

namespace NotificationService.Consumers;

// Premissa: Independência entre serviços — NotificationService não conhece Order nem Inventory
public class OrderStatusConsumer(
    ServiceBusClient busClient,
    IEmailService emailService,
    ILogger<OrderStatusConsumer> logger) : BackgroundService
{
    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = busClient.CreateProcessor("order-status-changed", new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls   = 20,
            AutoCompleteMessages = false
        });

        _processor.ProcessMessageAsync += HandleAsync;
        _processor.ProcessErrorAsync   += OnErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);
        logger.LogInformation("NotificationService iniciado — ouvindo 'order-status-changed'");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var evt = JsonSerializer.Deserialize<OrderStatusChangedEvent>(args.Message.Body.ToString())!;

            var (subject, body) = BuildEmail(evt);
            await emailService.SendAsync(evt.CustomerEmail, subject, body);

            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao enviar notificação");
            await args.AbandonMessageAsync(args.Message);
        }
    }

    private static (string subject, string body) BuildEmail(OrderStatusChangedEvent evt) =>
        evt.NewStatus switch
        {
            "InventoryReserved" => (
                "✅ Pedido confirmado!",
                $"Olá! Seu pedido {evt.OrderId} foi confirmado e está sendo processado."),
            "Rejected" => (
                "❌ Pedido não pode ser processado",
                $"Olá! Infelizmente o pedido {evt.OrderId} foi rejeitado por falta de estoque."),
            "Cancelled" => (
                "🚫 Pedido cancelado",
                $"Seu pedido {evt.OrderId} foi cancelado conforme solicitado."),
            _ => (
                $"📦 Atualização do pedido {evt.OrderId}",
                $"Status atualizado para: {evt.NewStatus}")
        };

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Erro no consumer de notificação");
        return Task.CompletedTask;
    }

    public override async ValueTask DisposeAsync()
    {
        if (_processor is not null) await _processor.DisposeAsync();
        await base.DisposeAsync();
    }
}
