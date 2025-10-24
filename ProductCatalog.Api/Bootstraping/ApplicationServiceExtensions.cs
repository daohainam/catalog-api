using Asp.Versioning;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Data;
using System;

namespace ProductCatalog.Api.Bootstraping;

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
    }
}
