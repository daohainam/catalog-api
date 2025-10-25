using EventBus.Kafka;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Data;
using ProductCatalog.OutboxService;
using ProductCatalog.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.AddKafkaProducer("kafka");
builder.AddKafkaEventPublisher("catalog-events");

builder.Services.AddSingleton(s => new TransactionalOutboxLogTailingServiceOptions()
{
    ConnectionString = builder.Configuration.GetConnectionString("catalogdb") ?? throw new InvalidOperationException("Connection string 'catalogdb' not found.")
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
