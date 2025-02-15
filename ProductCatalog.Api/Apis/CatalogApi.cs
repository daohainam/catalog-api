using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Api.Models;
using ProductCatalog.Api.Services;
using System.ComponentModel;

namespace ProductCatalog.Api.Apis;
public static class CatalogApi
{
    public static IEndpointRouteBuilder MapCatalogApi(this IEndpointRouteBuilder app)
    {
        // RouteGroupBuilder for catalog endpoints
        var vApi = app.NewVersionedApi("Catalog");
        var v1 = vApi.MapGroup("api/catalog").HasApiVersion(1, 0);

        // Routes for querying catalog items.
        v1.MapGet("/categories", GetAllCategories)
            .WithName("ListCategories")
            .WithSummary("List categories");

        v1.MapPost("/categories", CreateCategory)
            .WithName("CreateCategory")
            .WithSummary("Create a category")
            .WithDescription("Create a category");

        return app;
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Ok<PaginatedResult<Category>>> GetAllCategories(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("Id of parent, use Guid.Empty to get root categories")] Guid parentId
    )
    {
        var pageSize = paginationRequest.PageSize;
        var pageIndex = paginationRequest.PageIndex;

        var query = services.Context.Categories.Where(c => c.ParentId == parentId);

        var totalItems = await query
            .LongCountAsync();

        var itemsOnPage = await query
            .OrderBy(c => c.Name)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .Select(c => services.Mapper.Map<Category>(c))
            .ToListAsync();

        return TypedResults.Ok(new PaginatedResult<Category>(pageIndex, pageSize, totalItems, itemsOnPage));
    }

    public static async Task<Created> CreateCategory(
        [AsParameters] CatalogServices services,
        Category category)
    {
        var entity = services.Mapper.Map<ProductCatalog.Infrastructure.Entities.Category>(category);
        entity.Id = Guid.CreateVersion7();

        services.Context.Categories.Add(entity);
        await services.Context.SaveChangesAsync();

        return TypedResults.Created($"/api/catalog/categories/{entity.Id}");
    }
}
