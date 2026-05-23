namespace ProductCatalog.Api.Apis;

public static class VariantApi
{
    public static RouteGroupBuilder MapVariantApi(this RouteGroupBuilder group)
    {
        var productApiGroup = group.MapGroup("products").WithTags("Product");
        productApiGroup.MapGet("/{productId:guid}/variants", FindVariants);
        productApiGroup.MapPost("/{productId:guid}/variants", CreateVariant);
        productApiGroup.MapPut("/{productId:guid}/variants/{variantId:guid}", UpdateVariant);

        return group;
    }

    private static async Task<Results<Ok<Variant>, BadRequest, NotFound>> UpdateVariant([AsParameters] ApiServices services, Guid productId, Guid variantId, Variant variant)
    {
        if (variant == null || variant.Id != variantId)
        {
            return TypedResults.BadRequest();
        }
        var existingVariant = await services.DbContext.Variants.FindAsync(variantId);
        if (existingVariant == null || existingVariant.ProductId != productId)
        {
            return TypedResults.NotFound();
        }

        // do not update stock, it should be updated via inventory service only
        existingVariant.Sku = variant.Sku;
        existingVariant.BarCode = variant.BarCode;
        existingVariant.Price = variant.Price;
        existingVariant.Description = variant.Description;
        existingVariant.IsActive = variant.IsActive;
        existingVariant.IsDeleted = variant.IsDeleted;
        existingVariant.UpdatedAt = DateTime.UtcNow;

        services.DbContext.Variants.Update(existingVariant);
        await services.DbContext.SaveChangesAsync(services.CancellationToken);
        return TypedResults.Ok(existingVariant);
    }

    private static async Task<Results<Ok<Variant[]>, BadRequest, BadRequest<string>, NotFound>> CreateVariant([AsParameters] ApiServices services, Guid productId, Variant[] variants)
    {
        if (variants == null || variants.Length == 0)
        {
            return TypedResults.BadRequest();
        }

        var product = await services.DbContext.Products.Where(p => p.Id == productId).SingleOrDefaultAsync();
        if (product == null)
        {
            return TypedResults.NotFound();
        }

        var dimensions = await services.DbContext.ProductDimensions.Where(pd => pd.ProductId == productId)
                                    .Include(d => d.Dimension).ThenInclude(dv => dv.Values)
                                    .Select(pd => pd.Dimension)
                                    .ToListAsync(services.CancellationToken);

        var allDimensionValues = new List<VariantDimensionValue>();

        foreach (var variant in variants)
        {
            if (variant.Id == Guid.Empty)
                variant.Id = Guid.CreateVersion7();
            variant.ProductId = productId;
            variant.CreatedAt = DateTime.UtcNow;
            variant.UpdatedAt = DateTime.UtcNow;

            variant.Description ??= string.Empty;
            variant.BarCode ??= string.Empty;

            // validate dimension values
            if (dimensions.Count != variant.DimensionValues!.Count)
            {
                return TypedResults.BadRequest("All product dimensions must be specified for the variant.");
            }

            var variantDimLookup = variant.DimensionValues.ToDictionary(dv => dv.DimensionId);
            foreach (var dim in dimensions)
            {
                if (!variantDimLookup.TryGetValue(dim.Id, out var variantDimValue))
                {
                    return TypedResults.BadRequest($"Dimension '{dim.Id}' must be specified for the variant.");
                }
                if (!dim.Values.Any(v => v.Value == variantDimValue.Value))
                {
                    return TypedResults.BadRequest($"Invalid value '{variantDimValue.Value}' for dimension '{dim.Id}'.");
                }
            }

            foreach (var dimValue in variant.DimensionValues)
            {
                dimValue.VariantId = variant.Id;
                allDimensionValues.Add(dimValue);
            }
        }

        await services.DbContext.Variants.AddRangeAsync(variants, services.CancellationToken);
        if (allDimensionValues.Count > 0)
        {
            await services.DbContext.VariantDimensionValues.AddRangeAsync(allDimensionValues, services.CancellationToken);
        }

        await services.DbContext.SaveChangesAsync(services.CancellationToken);
        return TypedResults.Ok(variants);
    }

    private static async Task<Results<Ok<List<Variant>>, NotFound>> FindVariants([AsParameters] ApiServices services, Guid productId)
    {
        var productExists = await services.DbContext.Products.AnyAsync(p => p.Id == productId, services.CancellationToken);
        if (!productExists)
        {
            return TypedResults.NotFound();
        }

        var variants = await services.DbContext.Variants.AsNoTracking().Where(v => v.ProductId == productId).ToListAsync(services.CancellationToken);
        return TypedResults.Ok(variants);
    }
}
