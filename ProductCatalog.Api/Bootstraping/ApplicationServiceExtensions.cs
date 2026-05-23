using Asp.Versioning;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Data;
using System;
using System.Threading.RateLimiting;

namespace ProductCatalog.Api.Bootstraping;

public class RateLimitOptions
{
    public int PermitLimit { get; set; } = 100;
    public int WindowMinutes { get; set; } = 1;
    public int QueueLimit { get; set; } = 10;
}

public static class ApplicationServiceExtensions
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
        builder.Services.AddApiVersioning(options => {
            options.ReportApiVersions = true;
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

        var rateLimitOptions = new RateLimitOptions();
        builder.Configuration.GetSection("RateLimiting").Bind(rateLimitOptions);

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("fixed", limiterOptions =>
            {
                limiterOptions.PermitLimit = rateLimitOptions.PermitLimit;
                limiterOptions.Window = TimeSpan.FromMinutes(rateLimitOptions.WindowMinutes);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = rateLimitOptions.QueueLimit;
            });
        });
    }
}
