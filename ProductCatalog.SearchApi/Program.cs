using ProductCatalog.Search;
using ProductCatalog.SearchApi.Apis;
using ProductCatalog.SearchApi.Bootstraping;
using ProductCatalog.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddApplicationServices();

var app = builder.Build();

// Initialize Elasticsearch index with optimized mappings on startup
using (var scope = app.Services.CreateScope())
{
    var indexInitializer = scope.ServiceProvider.GetRequiredService<ElasticsearchIndexInitializer>();
    await indexInitializer.InitializeAsync(recreateIfExists: false);
}

app.MapDefaultEndpoints();
app.MapSearchApi();

app.Run();