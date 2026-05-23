using Microsoft.EntityFrameworkCore.Design;

namespace ProductCatalog.Infrastructure.Data
{
    public class ProductCatalogDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ProductCatalogDbContext>
    {
        public ProductCatalogDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ProductCatalogDbContext>();
            
            // This is only used for design-time operations (migrations, scaffolding).
            // Connection string MUST be provided via environment variable.
            var connectionString = Environment.GetEnvironmentVariable("PRODUCTCATALOG_CONNECTIONSTRING")
                ?? throw new InvalidOperationException(
                    "Connection string not found. Set the PRODUCTCATALOG_CONNECTIONSTRING environment variable. " +
                    "Example: Host=localhost;Database=productcatalog;Username=postgres;Password=yourpassword");
            
            optionsBuilder.UseNpgsql(connectionString);

            return new ProductCatalogDbContext(optionsBuilder.Options);
        }
    }
}
