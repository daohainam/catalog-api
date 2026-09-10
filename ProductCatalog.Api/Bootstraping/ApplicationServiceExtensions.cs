using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Data;
using System.Threading.RateLimiting;

namespace ProductCatalog.Api.Bootstraping;

public static class ApplicationServiceExtensions
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
        builder.Services.AddApiVersioning(options => {
            options.ReportApiVersions = true;
            // The route prefix is literal "/api/v1", so a request carries no
            // version segment; without a default, every request would fail to
            // resolve a version once the endpoints declare one.
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-Version"));
        });

        builder.AddNpgsqlDbContext<ProductCatalogDbContext>("catalogdb", configureDbContextOptions: dbContextOptionsBuilder =>
        {
            dbContextOptionsBuilder.UseNpgsql(builder =>
            {
            });
        });

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Partitioned per client: a single fixed-window bucket for the whole
            // process let one noisy caller lock out everyone else.
            options.AddPolicy("fixed", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10
                }));
        });
    }
}
