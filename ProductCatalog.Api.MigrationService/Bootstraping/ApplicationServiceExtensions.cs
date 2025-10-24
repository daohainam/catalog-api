using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Data;

namespace ProductCatalog.Api.Bootstraping;

public static class ApplicationServiceExtensions
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<ProductCatalogDbContext>("catalogdb", configureDbContextOptions: dbContextOptionsBuilder =>
        {
            dbContextOptionsBuilder.UseNpgsql(builder =>
            {
                builder.MigrationsAssembly(typeof(ProductCatalogDbContext).Assembly.FullName);
            });
        });
    }
}
