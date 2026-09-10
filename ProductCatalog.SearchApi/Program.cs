using ProductCatalog.Search;
using ProductCatalog.SearchApi.Apis;
using ProductCatalog.SearchApi.Bootstraping;
using ProductCatalog.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddApplicationServices();

var app = builder.Build();

// Initialize Elasticsearch index with optimized mappings on startup
try
{
    using var scope = app.Services.CreateScope();
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
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Failed to initialize Elasticsearch index. Application will not start.");
    throw;
}

app.MapDefaultEndpoints();
app.UseRateLimiter();
app.MapSearchApi();

app.Run();