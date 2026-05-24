using Azure.Messaging.ServiceBus;
using NotificationService.Consumers;
using NotificationService.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(_ =>
    new ServiceBusClient(builder.Configuration["ServiceBus:ConnectionString"]));

builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddHostedService<OrderStatusConsumer>();

builder.Services.AddApplicationInsightsTelemetryWorkerService();

var host = builder.Build();
host.Run();
