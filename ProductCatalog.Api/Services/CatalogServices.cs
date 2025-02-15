using AutoMapper;
using ProductCatalog.Infrastructure.Data;

namespace ProductCatalog.Api.Services
{
    public class CatalogServices(
        CatalogContext context,
        IMapper mapper,
        ILogger<CatalogServices> logger) {
        public CatalogContext Context => context;
        public IMapper Mapper => mapper;
        public ILogger<CatalogServices> Logger => logger;
    }
}
