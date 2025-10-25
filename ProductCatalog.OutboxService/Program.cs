using EventBus.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProductCatalog.Events;
using ProductCatalog.Infrastructure.Data;
using ProductCatalog.OutboxService;
using ProductCatalog.ServiceDefaults;
using System.Reflection;

var builder = Host.CreateApplicationBuilder(args);
var eventAssembly = typeof(ProductCreatedEvent).Assembly;

builder.AddServiceDefaults();

builder.AddKafkaProducer("kafka");
builder.AddKafkaEventPublisher("catalog-events");

builder.Services.AddSingleton(s => new TransactionalOutboxLogTailingServiceOptions()
{
    ConnectionString = builder.Configuration.GetConnectionString("catalogdb") ?? throw new InvalidOperationException("Connection string 'catalogdb' not found."),
    PayloadTypeResolver = (type) => (eventAssembly ?? Assembly.GetExecutingAssembly()).GetType(type) ?? throw new Exception($"Could not get type {type}"),
});

builder.AddNpgsqlDbContext<ProductCatalogDbContext>("catalogdb", configureDbContextOptions: dbContextOptionsBuilder =>
{
    dbContextOptionsBuilder.UseNpgsql(builder =>
    {
    });
}); 

builder.Services.AddHostedService<TransactionalOutboxLogTailingService>();

var host = builder.Build();
host.Run();
