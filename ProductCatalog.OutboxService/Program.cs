using EventBus.Kafka;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Events;
using ProductCatalog.Infrastructure.Data;
using ProductCatalog.OutboxService;
using ProductCatalog.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);
var eventAssembly = typeof(ProductCreatedEvent).Assembly;

builder.AddServiceDefaults();

builder.AddKafkaProducer("kafka");
builder.AddKafkaEventPublisher("catalog-events");

builder.Services.AddSingleton(s =>
{
    var connectionString = builder.Configuration.GetConnectionString("catalogdb");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Connection string 'catalogdb' not found or is empty.");
    }

    return new TransactionalOutboxLogTailingServiceOptions()
    {
        ConnectionString = connectionString,
        // Returns null rather than throwing: the caller logs the unknown type and
        // parks the row. Throwing here took the whole service down on one bad row.
        PayloadTypeResolver = eventAssembly.GetType,
    };
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
