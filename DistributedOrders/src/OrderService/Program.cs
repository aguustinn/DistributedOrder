using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using OrderService.Consumers;
using OrderService.Data;
using OrderService.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Banco de dados (Azure SQL) ────────────────────────────────────────────────
builder.Services.AddDbContext<OrderDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("OrdersDb")));

// ── Cache Distribuído (Redis) — Premissa: Transparência de Acesso ─────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]!));

// ── Mensageria assíncrona (Azure Service Bus) ─────────────────────────────────
builder.Services.AddSingleton(_ =>
    new ServiceBusClient(builder.Configuration["ServiceBus:ConnectionString"]));

// ── Serviços de domínio ───────────────────────────────────────────────────────
builder.Services.AddScoped<IOrderService, OrderAppService>();

// ── Consumer em background (escuta respostas do InventoryService) ─────────────
builder.Services.AddHostedService<InventoryResponseConsumer>();

// ── API ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "Order Service", Version = "v1" }));

// ── Observabilidade (Application Insights) ────────────────────────────────────
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// Aplica migrations automaticamente ao iniciar (conveniente para labs/academico)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await db.Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();
