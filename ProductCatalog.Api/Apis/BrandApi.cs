namespace ProductCatalog.Api.Apis;

public static class BrandApi
{
    public static RouteGroupBuilder MapBrandApi(this RouteGroupBuilder group)
    {
        var brandApiGroup = group.MapGroup("brands").WithTags("Brand");
        brandApiGroup.MapPost("/", CreateBrand);
        brandApiGroup.MapGet("/", FindBrands);
        brandApiGroup.MapGet("/{brandId:guid}", FindBrandById);
        brandApiGroup.MapPut("/{brandId:guid}", UpdateBrand);

        return group;
    }

    private static async Task<Results<Ok<Brand>, BadRequest, BadRequest<string>>> UpdateBrand([AsParameters] ApiServices services, Guid brandId, Brand brand)
    {
        if (brand == null || brand.Id != brandId)
        {
            return TypedResults.BadRequest();
        }

        var existingBrand = await services.DbContext.Brands.FindAsync(brandId);
        if (existingBrand == null)
        {
            return TypedResults.BadRequest("Brand not found.");
        }

        if (string.IsNullOrEmpty(brand.Name))
        {
            return TypedResults.BadRequest("Brand Name is required.");
        }

        if (string.IsNullOrEmpty(brand.UrlSlug))
        {
            return TypedResults.BadRequest("Brand UrlSlug is required.");
        }

        existingBrand.Name = brand.Name;
        existingBrand.Description = brand.Description;
        existingBrand.UrlSlug = brand.UrlSlug;
        existingBrand.LogoUrl = brand.LogoUrl;

        services.DbContext.Brands.Update(existingBrand);
        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok(existingBrand);
    }

    private static async Task<Results<Ok<Brand>, NotFound>> FindBrandById([AsParameters] ApiServices services, Guid brandId)
    {
        var brand = await services.DbContext.Brands.FindAsync(brandId);
        if (brand == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(brand);
    }

    private static async Task<Results<Ok<Brand[]>, NotFound>> FindBrands([AsParameters] ApiServices services, [FromQuery] int? offset = 0, [FromQuery] int? limit = PaginationDefaults.DefaultPageSize)
    {
        var (validatedOffset, validatedLimit) = PaginationHelper.ValidatePagination(offset, limit);
        var brands = await services.DbContext.Brands
            .AsNoTracking()
            .OrderBy(b => b.Id)
            .Skip(validatedOffset).Take(validatedLimit)
            .ToArrayAsync(services.CancellationToken);

        return TypedResults.Ok(brands);
    }

    private static async Task<Results<Ok<Brand>, BadRequest, BadRequest<string>>> CreateBrand([AsParameters] ApiServices services, Brand brand)
    {
        if (brand == null)
        {
            return TypedResults.BadRequest("Brand object is required.");
        }

        if (string.IsNullOrWhiteSpace(brand.Name))
        {
            return TypedResults.BadRequest("Brand Name is required and cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(brand.UrlSlug))
        {
            return TypedResults.BadRequest("Brand UrlSlug is required and cannot be empty.");
        }

        if (brand.Name.Length > 200)
        {
            return TypedResults.BadRequest("Brand Name cannot exceed 200 characters.");
        }

        if (brand.UrlSlug.Length > 200)
        {
            return TypedResults.BadRequest("Brand UrlSlug cannot exceed 200 characters.");
        }

        if (brand.Id == Guid.Empty)
            brand.Id = Guid.CreateVersion7();

        await services.DbContext.Brands.AddAsync(brand);
        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok(brand);
    }
}
