using ProductCatalog.Search;
using ProductCatalog.ServiceDefaults;

namespace ProductCatalog.SearchApi.Bootstraping;
public static class ApplicationServiceExtensions
{
    public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.AddServiceDefaults();

        if (builder.Environment.IsDevelopment())
            builder.Services.AddOpenApi();

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

        // Register index initializer for creating optimized index
        builder.Services.AddSingleton<ElasticsearchIndexInitializer>();

        return builder;
    }
}
