using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Data;

namespace ProductCatalog.Api.Bootstraping
{
    public static class ApplicationServiceExtensions
    {
        public static void AddApplicationServices(this IHostApplicationBuilder builder)
        {
            builder.Services.AddOpenApi();
            builder.Services.AddApiVersioning();
            builder.Services.AddAutoMapper(typeof(ModelProfile));

            builder.AddNpgsqlDbContext<CatalogContext>("catalogdb", configureDbContextOptions: dbContextOptionsBuilder =>
            {
                dbContextOptionsBuilder.UseNpgsql(builder =>
                {
                });
            });
        }
    }
}
