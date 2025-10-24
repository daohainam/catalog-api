using ProductCatalog.Infrastructure.Data;

namespace ProductCatalog.Api.Services;

public class ApiServices(
    ProductCatalogDbContext dbContext, CancellationToken cancellationToken)
{
    public ProductCatalogDbContext DbContext => dbContext;
    public CancellationToken CancellationToken => cancellationToken;
}
