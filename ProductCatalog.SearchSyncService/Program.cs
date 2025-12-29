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

// Register index configuration options
var indexOptions = new ElasticsearchIndexOptions();
builder.Configuration.GetSection("Elasticsearch:Index").Bind(indexOptions);
builder.Services.AddSingleton(indexOptions);

// Register index initializer for creating optimized index on startup
builder.Services.AddSingleton<ElasticsearchIndexInitializer>();

// Register event handlers
builder.Services.AddSingleton<IEventHandlerFactory, EventHandlerFactory>();
builder.Services.AddTransient<ProductCreatedEventHandler>();

// Register the event handling background service
builder.Services.AddHostedService<EventHandlingService>();

var host = builder.Build();

// Initialize Elasticsearch index with optimized mappings on startup
try
{
    using var scope = host.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var indexInitializer = scope.ServiceProvider.GetRequiredService<ElasticsearchIndexInitializer>();
    
    logger.LogInformation("Initializing Elasticsearch index...");
    await indexInitializer.InitializeAsync(recreateIfExists: false);
    logger.LogInformation("Elasticsearch index initialized successfully");
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Failed to initialize Elasticsearch index. Application will not start.");
    throw;
}

host.Run();
