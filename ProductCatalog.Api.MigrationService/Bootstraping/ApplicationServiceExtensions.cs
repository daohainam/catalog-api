using Microsoft.EntityFrameworkCore;
using ProductCatalog.Api.Infrastructure.Data;

namespace ProductCatalog.Api.Bootstraping
{
    public static class ApplicationServiceExtensions
    {
        public static void AddApplicationServices(this IHostApplicationBuilder builder)
        {
            builder.AddNpgsqlDbContext<CatalogContext>("catalogdb", configureDbContextOptions: dbContextOptionsBuilder =>
            {
                dbContextOptionsBuilder.UseNpgsql(builder =>
                {
                });
            });
        }
    }
}
