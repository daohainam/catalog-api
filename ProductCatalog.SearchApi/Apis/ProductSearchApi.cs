using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Search;

namespace ProductCatalog.SearchApi.Apis;
public static class ProductSearchApi
{
    private const int defaultPageSize = 10;
    private const int maxPageSize = 100;
    
    public static IEndpointRouteBuilder MapSearchApi(this IEndpointRouteBuilder builder)
    {
        builder.MapGroup("/api/v1")
              .MapSearchApi()
              .WithTags("Product Search Api");

        return builder;
    }

    public static RouteGroupBuilder MapSearchApi(this RouteGroupBuilder group)
    {
        var productApiGroup = group.MapGroup("products").WithTags("Product");

        productApiGroup.MapGet("/search", FullTextSearchProducts);

        return group;
    }

    /// <summary>
    /// Full-text search across product name, description, and brand.
    /// Leverages optimized Elasticsearch mappings:
    /// - Text fields with keyword subfields for sorting
    /// - Keyword fields for exact-match filtering
    /// - Nested variants for complex product variant queries
    /// - Scaled float for efficient price storage
    /// </summary>
    private static async Task<IResult> FullTextSearchProducts(
        [AsParameters] ApiServices apiServices, 
        [FromQuery] string? query, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = defaultPageSize)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.BadRequest("Query parameter is required.");
        }

        page = page < 1 ? 1 : page;
        pageSize = Math.Min(pageSize < 1 ? defaultPageSize : pageSize, maxPageSize);

        // Uses optimized index with:
        // - 3 shards for distributed query load
        // - Keyword types for fast filtering
        // - Text + keyword multi-field for sort/filter
        // - 30s refresh interval for high indexing throughput
        var searchResponse = await apiServices.Client.SearchAsync<ProductIndexDocument>(s => s
            .From((page - 1) * pageSize)
            .Size(pageSize)
            .Query(q => q
                .Bool(b => b
                    .Must(m => m
                        .QueryString(qs => qs
                            .Query(query)
                        )
                    )
                    .Filter(f => f
                        .Term(t => t.Field("is_active").Value(true))
                    )
                )
            ),
            apiServices.CancellationToken
        );

        if (!searchResponse.IsValidResponse)
        {
            return Results.Problem(
                title: "Search failed",
                detail: searchResponse.DebugInformation,
                statusCode: 500
            );
        }

        var result = new
        {
            Total = searchResponse.Total,
            Page = page,
            PageSize = pageSize,
            Products = searchResponse.Documents
        };

        return Results.Ok(result);
    }
}
