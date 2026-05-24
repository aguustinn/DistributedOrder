using Azure.Messaging.ServiceBus;
using InventoryService.Consumers;
using InventoryService.Data;
using InventoryService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<InventoryDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("InventoryDb")));

builder.Services.AddSingleton(_ =>
    new ServiceBusClient(builder.Configuration["ServiceBus:ConnectionString"]));

builder.Services.AddScoped<IInventoryService, InventoryAppService>();
builder.Services.AddHostedService<OrderCreatedConsumer>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "Inventory Service", Version = "v1" }));
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await db.Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();
