using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Api.Models;
using ProductCatalog.Api.Services;
using System.ComponentModel;

namespace ProductCatalog.Api.Apis;
public static class CatalogApi
{
    private const string ApiName = "api/catalog";

    public static IEndpointRouteBuilder MapCatalogApi(this IEndpointRouteBuilder app)
    {
        // RouteGroupBuilder for catalog endpoints
        var vApi = app.NewVersionedApi("Catalog");
        var v1 = vApi.MapGroup("api/catalog/v{version:apiVersion}").HasApiVersion(1, 0);

        // Routes for querying catalog items.
        v1.MapGet("/categories", GetAllCategories)
            .WithName("ListCategories")
            .WithSummary("List categories");

        v1.MapPost("/categories", CreateCategory)
            .WithName("CreateCategory")
            .WithSummary("Create a category")
            .WithDescription("Create a category");

        v1.MapGet("/brands", GetAllBrands)
            .WithName("ListBrands")
            .WithSummary("List brands");

        v1.MapPost("/brands", CreateBrand)
            .WithName("CreateBrand")
            .WithSummary("Create a brand")
            .WithDescription("Create a brand");

        v1.MapGet("/products/{id:guid}", GetProductById)
            .WithName("GetProductById")
            .WithSummary("Get product by Id");

        v1.MapGet("/products", GetAllProducts)
            .WithName("GetProducts")
            .WithSummary("Get products");

        v1.MapPost("/products", CreateProduct)
            .WithName("CreateProduct")
            .WithSummary("Create a product")
            .WithDescription("Create a product");

        v1.MapGet("/dimensions", GetProductDimensions)
            .WithName("GetProductDimensions")
            .WithSummary("Get product dimensions");
        v1.MapPost("/dimensions", CreateDimension)
            .WithName("CreateDimension")
            .WithSummary("Create a dimension with values");
        v1.MapPost("/dimensions/{id:guid}/values", CreateDimensionValues)
            .WithName("CreateDimensionValue")
            .WithSummary("Create a value for a dimension");


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
        CategoryCreate category)
    {
        var entity = services.Mapper.Map<Infrastructure.Entities.Category>(category);
        entity.Id = Guid.CreateVersion7();

        services.Context.Categories.Add(entity);
        await services.Context.SaveChangesAsync();

        return TypedResults.Created($"/api/catalog/categories/{entity.Id}");
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Ok<PaginatedResult<Brand>>> GetAllBrands(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services
    )
    {
        var pageSize = paginationRequest.PageSize;
        var pageIndex = paginationRequest.PageIndex;

        var query = services.Context.Brands;

        var totalItems = await query
            .LongCountAsync();

        var itemsOnPage = await query
            .OrderBy(c => c.Name)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .Select(c => services.Mapper.Map<Brand>(c))
            .ToListAsync();

        return TypedResults.Ok(new PaginatedResult<Brand>(pageIndex, pageSize, totalItems, itemsOnPage));
    }

    public static async Task<Created> CreateBrand(
        [AsParameters] CatalogServices services,
        BrandCreate brand)
    {
        var entity = services.Mapper.Map<Infrastructure.Entities.Brand>(brand);
        entity.Id = Guid.CreateVersion7();

        services.Context.Brands.Add(entity);
        await services.Context.SaveChangesAsync();

        return TypedResults.Created($"/api/catalog/brands/{entity.Id}");
    }

    private static async Task<Results<Ok<Product>, NotFound, BadRequest<ProblemDetails>>> GetProductById (
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The product item id")] Guid id
    )
    {
        var entity = await services.Context.Products
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(ci => ci.Variants)
            .SingleOrDefaultAsync(ci => ci.Id == id);

        if (entity == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(services.Mapper.Map<Product>(entity));
    }

    public static async Task<Ok<PaginatedResult<Product>>> GetAllProducts(
    [AsParameters] PaginationRequest paginationRequest,
    [AsParameters] CatalogServices services,
    [Description("The type of items to return")] Guid? category,
    [Description("The brand of items to return")] Guid? brand)
    {
        var pageSize = paginationRequest.PageSize;
        var pageIndex = paginationRequest.PageIndex;

        var root = (IQueryable<Product>)services.Context.Products;

        if (category is not null)
        {
            root = root.Where(c => c.CategoryId == category);
        }
        if (brand is not null)
        {
            root = root.Where(c => c.BranchId == brand);
        }

        var totalItems = await root
            .LongCountAsync();

        var itemsOnPage = await root
            .OrderBy(c => c.Name)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToListAsync();

        return TypedResults.Ok(new PaginatedResult<Product>(pageIndex, pageSize, totalItems, itemsOnPage));
    }

    public static async Task<Created> CreateProduct(
    [AsParameters] CatalogServices services,
    ProductCreate product)
    {
        var entity = services.Mapper.Map<Infrastructure.Entities.Product>(product);
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsPublished = false;
        entity.IsDeleted = false;

        services.Context.Products.Add(entity);
        await services.Context.SaveChangesAsync();

        return TypedResults.Created($"/api/catalog/products/{entity.Id}");
    }

    public static async Task<Results<Ok<PaginatedResult<Dimension>>, NotFound>> GetProductDimensions(
    [AsParameters] PaginationRequest paginationRequest,
    [AsParameters] CatalogServices services,
    [Description("Product id")] Guid id)
    {
        var pageSize = paginationRequest.PageSize;
        var pageIndex = paginationRequest.PageIndex;

        var entity = await services.Context.Products.Include(p => p.Dimensions).Where(p => p.Id == id).SingleOrDefaultAsync();

        if (entity == null)
        {
            return TypedResults.NotFound();
        }

        var itemsOnPage = entity.Dimensions
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .Select(d => services.Mapper.Map<Dimension>(d))
            .ToList();

        return TypedResults.Ok(new PaginatedResult<Dimension>(pageIndex, pageSize, entity.Dimensions.LongCount(), itemsOnPage));
    }

    public static async Task<Created> CreateDimension(
        [AsParameters] CatalogServices services,
        DimensionCreate dimension)
    { 
        var entity = services.Mapper.Map<Infrastructure.Entities.Dimension>(dimension);

        services.Context.Dimensions.Add(entity);
        await services.Context.SaveChangesAsync();

        return TypedResults.Created($"/api/catalog/products/{entity.Id}");
    }

    public static async Task<Results<Created, NotFound>> CreateDimensionValues(
        [AsParameters] CatalogServices services,
        [Description("Dimension id")] Guid id,
        List<DimensionValueCreate> dimensionValues)
    {
        var entity = await services.Context.Dimensions.SingleOrDefaultAsync(d => d.Id == id);

        if (entity == null)
        {
            return TypedResults.NotFound();
        }

        // Assuming you need to add dimension values to the entity
        foreach (var value in dimensionValues)
        {
            var dimensionValueEntity = services.Mapper.Map<Infrastructure.Entities.DimensionValue>(value);
            entity.DimensionValues.Add(dimensionValueEntity);
        }

        await services.Context.SaveChangesAsync();

        return TypedResults.Created($"/api/catalog/dimensions/{entity.Id}/values");
    }
}
