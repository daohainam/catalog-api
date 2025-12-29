using EventBus;
using ProductCatalog.Events;
using ProductCatalog.Search;
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

builder.AddElasticsearchClient(connectionName: "elasticsearch",
    configureClientSettings: (settings) =>
    {
        settings.DefaultMappingFor<ProductIndexDocument>(m => m.IndexName(nameof(ProductIndexDocument).ToLower()));
    }
);

// Register index initializer for creating optimized index on startup
builder.Services.AddSingleton<ElasticsearchIndexInitializer>();

// Register event handlers
builder.Services.AddSingleton<IEventHandlerFactory, EventHandlerFactory>();
builder.Services.AddTransient<ProductCreatedEventHandler>();

// Register the event handling background service
builder.Services.AddHostedService<EventHandlingService>();

var host = builder.Build();

// Initialize Elasticsearch index with optimized mappings on startup
using (var scope = host.Services.CreateScope())
{
    var indexInitializer = scope.ServiceProvider.GetRequiredService<ElasticsearchIndexInitializer>();
    await indexInitializer.InitializeAsync(recreateIfExists: false);
}

host.Run();
