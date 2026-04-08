using Microsoft.AspNetCore.RateLimiting;
using ProductCatalog.Search;
using ProductCatalog.ServiceDefaults;
using System.Threading.RateLimiting;

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

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("fixed", limiterOptions =>
            {
                limiterOptions.PermitLimit = 100;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 10;
            });
        });

        return builder;
    }
}
