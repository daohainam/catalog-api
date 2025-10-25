using EventBus;
using ProductCatalog.Events;
using ProductCatalog.SearchSyncService;
using ProductCatalog.SearchSyncService.EventHandlers;
using ProductCatalog.SearchSyncService.Extensions;
using ProductCatalog.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddKafkaEventConsumer(options =>
{
    options.ServiceName = "CatalogSyncService";
    options.KafkaGroupId = "catalog-service";
    options.Topics.AddRange("catalog-events");
    options.IntegrationEventFactory = IntegrationEventFactory<ProductCreatedEvent>.Instance;
}); 

builder.Services.AddSingleton<IEventHandlerFactory, EventHandlerFactory>();
builder.Services.AddTransient<ProductCreatedEventHandler>();
builder.Services.AddHostedService<EventHandlingService>();

var host = builder.Build();
host.Run();
