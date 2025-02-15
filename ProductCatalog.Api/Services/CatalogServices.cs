using ProductCatalog.Api.Infrastructure.Data;

namespace ProductCatalog.Api.Services
{
    public class CatalogServices(
        CatalogContext context,
        ILogger<CatalogServices> logger) {
        public CatalogContext Context => context;
        public ILogger<CatalogServices> Logger => logger;
    }
}
