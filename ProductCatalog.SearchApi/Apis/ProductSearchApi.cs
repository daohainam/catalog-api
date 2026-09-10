using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using ProductCatalog.Search;

namespace ProductCatalog.SearchApi.Apis;
public static class ProductSearchApi
{
    private const int defaultPageSize = 10;
    private const int maxPageSize = 100;
    private const int maxResultWindow = 10_000; // Elasticsearch index.max_result_window default
    
    public static IEndpointRouteBuilder MapSearchApi(this IEndpointRouteBuilder builder)
    {
        builder.MapGroup("/api/v1")
              .MapSearchApi()
              .WithTags("Product Search Api")
              .RequireRateLimiting("fixed");

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

        pageSize = Math.Min(pageSize < 1 ? defaultPageSize : pageSize, maxPageSize);

        // from + size must stay under Elasticsearch's index.max_result_window, and
        // clamping page also stops (page - 1) * pageSize from overflowing to a
        // negative offset.
        var maxPage = Math.Max(1, maxResultWindow / pageSize);
        page = Math.Clamp(page, 1, maxPage);

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
                        // SimpleQueryString over an explicit field list, not
                        // QueryString: the latter hands anonymous callers full
                        // Lucene syntax (leading wildcards, regex, field probes)
                        // and turns a typo into a 500.
                        .SimpleQueryString(qs => qs
                            .Query(query)
                            .Fields(new[] { "name^3", "description", "brand_name^2", "category_name" })
                            .DefaultOperator(Operator.And)
                            .Lenient(true)
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
            apiServices.Logger.LogError("Elasticsearch search failed: {DebugInfo}", searchResponse.DebugInformation);

            var detail = apiServices.Environment.IsDevelopment()
                ? searchResponse.DebugInformation
                : "An internal search error occurred. Please try again later.";

            return Results.Problem(
                title: "Search failed",
                detail: detail,
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
