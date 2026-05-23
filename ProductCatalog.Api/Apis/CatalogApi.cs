namespace ProductCatalog.Api.Apis;

/// <summary>
/// Main API entry point that delegates to domain-specific API classes.
/// </summary>
public static class CatalogApi
{
    public static IEndpointRouteBuilder MapCatalogApi(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/v1")
              .WithTags("Product Catalog Api")
              .RequireRateLimiting("fixed");

        group.MapBrandApi();
        group.MapCategoryApi();
        group.MapGroupApi();
        group.MapDimensionApi();
        group.MapProductApi();
        group.MapVariantApi();

        return builder;
    }
}
