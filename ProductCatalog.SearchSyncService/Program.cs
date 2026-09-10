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
    // Resolves every event type declared in ProductCatalog.Events, not just one.
    options.IntegrationEventFactory = IntegrationEventFactory<ProductCreatedEvent>.Instance;
});

builder.AddElasticsearchClient(connectionName: "elasticsearch",
    configureClientSettings: (settings) =>
    {
        settings.DefaultMappingFor<ProductIndexDocument>(m => m
            .IndexName(ElasticsearchIndexConfiguration.IndexName)
            .IdProperty(p => p.ProductId));
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
builder.Services.AddTransient<ProductIndexWriter>();
builder.Services.AddTransient<ProductCreatedEventHandler>();
builder.Services.AddTransient<ProductUpdatedEventHandler>();
builder.Services.AddTransient<ProductDeletedEventHandler>();

var host = builder.Build();

// Initialize Elasticsearch index with optimized mappings on startup
try
{
    using var scope = host.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var indexInitializer = scope.ServiceProvider.GetRequiredService<ElasticsearchIndexInitializer>();

    logger.LogInformation("Initializing Elasticsearch index...");
    // Bounded so an unreachable Elasticsearch fails startup instead of hanging it.
    using var initializationTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    await indexInitializer.InitializeAsync(recreateIfExists: false, initializationTimeout.Token);
    logger.LogInformation("Elasticsearch index initialized successfully");
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Failed to initialize Elasticsearch index. Application will not start.");
    throw;
}

host.Run();
