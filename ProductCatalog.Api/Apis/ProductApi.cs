namespace ProductCatalog.Api.Apis;

public static class ProductApi
{
    public static RouteGroupBuilder MapProductApi(this RouteGroupBuilder group)
    {
        var productApiGroup = group.MapGroup("products").WithTags("Product");

        productApiGroup.MapPost("/", CreateProduct);
        productApiGroup.MapPut("/{productId:guid}", UpdateProduct);
        productApiGroup.MapGet("/", async ([AsParameters] ApiServices services, [FromQuery] int? offset = 0, [FromQuery] int? limit = PaginationDefaults.DefaultPageSize) =>
        {
            var (validatedOffset, validatedLimit) = PaginationHelper.ValidatePagination(offset, limit);
            return await services.DbContext.Products
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Skip(validatedOffset).Take(validatedLimit).ToListAsync(services.CancellationToken);
        });
        productApiGroup.MapGet("/{productId:guid}", async ([AsParameters] ApiServices services, Guid productId) =>
        {
            return await services.DbContext.Products.AsNoTracking().Include(p => p.Variants).ThenInclude(d => d.DimensionValues).Where(p => p.Id == productId).SingleOrDefaultAsync(services.CancellationToken);
        });
        productApiGroup.MapGet("/{productId:guid}/dimensions", async ([AsParameters] ApiServices services, Guid productId) =>
        {
            var dimensions = from pd in services.DbContext.ProductDimensions
                             join d in services.DbContext.Dimensions on pd.DimensionId equals d.Id
                             where pd.ProductId == productId
                             select d;

            return await dimensions.AsNoTracking().Include(d => d.Values).ToListAsync(services.CancellationToken);
        });
        productApiGroup.MapPost("/{productId:guid}/dimensions", AddProductDimension);

        productApiGroup.MapGet("/{productId:guid}/history", GetProductHistory);
        productApiGroup.MapGet("/{productId:guid}/history/{version:long}", GetProductHistoryByVersion);
        productApiGroup.MapPost("/{productId:guid}/history/{version:long}/revert", RevertProduct);

        return group;
    }

    private static async Task<Results<Ok<Product>, BadRequest, BadRequest<string>>> CreateProduct([AsParameters] ApiServices services, Product product)
    {
        if (product == null)
        {
            return TypedResults.BadRequest();
        }

        if (product.Id == Guid.Empty)
            product.Id = Guid.CreateVersion7();

        if (product.Groups != null && product.Groups.Count > 0)
        {
            return TypedResults.BadRequest("Use ProductGroups to assign groups to product.");
        }

        if (product.GroupProducts != null && product.GroupProducts.Count > 0)
        {
            foreach (var group in product.GroupProducts)
            {
                if (group.GroupId == Guid.Empty)
                    return TypedResults.BadRequest("Group Id is required.");
                group.ProductId = product.Id;
            }
        }

        product.CreatedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;
        product.Version = 1;
        product.UrlSlug ??= product.Id.ToString();

        if (product.Dimensions != null && product.Dimensions.Count > 0)
        {
            foreach (var dimension in product.Dimensions)
            {
                if (string.IsNullOrEmpty(dimension.DimensionId))
                    return TypedResults.BadRequest("Dimension Id is required.");
            }
        }

        if (product.Variants != null && product.Variants.Count > 0)
        {
            var productDimIds = product.Dimensions != null
                ? new HashSet<string>(product.Dimensions.Select(d => d.DimensionId))
                : new HashSet<string>();

            foreach (var variant in product.Variants)
            {
                if (variant.Id == Guid.Empty)
                    variant.Id = Guid.CreateVersion7();
                variant.ProductId = product.Id;
                variant.CreatedAt = DateTime.UtcNow;
                variant.UpdatedAt = DateTime.UtcNow;
                variant.Description ??= string.Empty;
                variant.BarCode ??= string.Empty;
                variant.Sku ??= string.Empty;

                if (variant.DimensionValues != null && variant.DimensionValues.Count > 0)
                {
                    if (product.Dimensions == null || product.Dimensions.Count != variant.DimensionValues.Count)
                    {
                        return TypedResults.BadRequest("All product dimensions must be specified for the variant.");
                    }

                    var variantDimIds = new HashSet<string>(variant.DimensionValues.Select(dv => dv.DimensionId));
                    if (!productDimIds.SetEquals(variantDimIds))
                    {
                        return TypedResults.BadRequest("All product dimensions must be specified for the variant.");
                    }

                    foreach (var dimValue in variant.DimensionValues)
                    {
                        dimValue.VariantId = variant.Id;
                    }
                }
            }
        }

        // Create outbox event - both outbox message and product are saved in a single SaveChangesAsync call
        // ensuring atomicity (Critical Fix #1: transaction splitting)
        var evt = await product.ToProductCreatedEvent(services);

        var payloadType = typeof(ProductCatalog.Events.ProductCreatedEvent).FullName
            ?? throw new InvalidOperationException($"Could not get full name of type {typeof(ProductCatalog.Events.ProductCreatedEvent)}");

        await services.DbContext.AddAsync(new LogTailingOutboxMessage()
        {
            Id = Guid.NewGuid(),
            CreationDate = DateTime.UtcNow,
            PayloadType = payloadType,
            Payload = JsonSerializer.Serialize(evt),
        });

        await services.DbContext.Products.AddAsync(product);
        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok(product);
    }

    private static async Task<Results<NotFound, Ok>> UpdateProduct([AsParameters] ApiServices services, Guid productId, Product product)
    {
        var existingProduct = await services.DbContext.Products.FindAsync(productId);
        if (existingProduct == null)
        {
            return TypedResults.NotFound();
        }

        // Save current product data to history before updating
        var historyData = new
        {
            existingProduct.Id,
            existingProduct.Name,
            existingProduct.UrlSlug,
            existingProduct.Description,
            existingProduct.BrandId,
            existingProduct.CategoryId,
            existingProduct.CreatedAt,
            existingProduct.UpdatedAt,
            existingProduct.IsActive,
            existingProduct.IsDeleted,
            existingProduct.Version
        };

        var history = new ProductHistory
        {
            Id = Guid.CreateVersion7(),
            ProductId = existingProduct.Id,
            Version = existingProduct.Version,
            ProductData = JsonSerializer.Serialize(historyData),
            CreatedAt = DateTime.UtcNow
        };

        await services.DbContext.ProductHistories.AddAsync(history, services.CancellationToken);

        existingProduct.Name = product.Name;
        existingProduct.Description = product.Description;
        existingProduct.IsActive = product.IsActive;
        existingProduct.UpdatedAt = DateTime.UtcNow;
        existingProduct.CategoryId = product.CategoryId;
        existingProduct.Version++;

        services.DbContext.Products.Update(existingProduct);

        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok();
    }

    #region Product History
    private static async Task<Results<Ok<List<ProductHistory>>, NotFound>> GetProductHistory(
        [AsParameters] ApiServices services, Guid productId, [FromQuery] int? offset = 0, [FromQuery] int? limit = PaginationDefaults.DefaultPageSize)
    {
        var productExists = await services.DbContext.Products.AnyAsync(p => p.Id == productId, services.CancellationToken);
        if (!productExists)
        {
            return TypedResults.NotFound();
        }

        var (validatedOffset, validatedLimit) = PaginationHelper.ValidatePagination(offset, limit);

        var history = await services.DbContext.ProductHistories
            .AsNoTracking()
            .Where(h => h.ProductId == productId)
            .OrderByDescending(h => h.Version)
            .Skip(validatedOffset)
            .Take(validatedLimit)
            .ToListAsync(services.CancellationToken);

        return TypedResults.Ok(history);
    }

    private static async Task<Results<Ok<ProductHistory>, NotFound>> GetProductHistoryByVersion(
        [AsParameters] ApiServices services, Guid productId, long version)
    {
        var history = await services.DbContext.ProductHistories
            .AsNoTracking()
            .Where(h => h.ProductId == productId && h.Version == version)
            .SingleOrDefaultAsync(services.CancellationToken);

        if (history == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(history);
    }

    // Critical Fix #3: Safe JSON property access using TryGetProperty
    private static async Task<Results<Ok<Product>, NotFound, BadRequest<string>>> RevertProduct(
        [AsParameters] ApiServices services, Guid productId, long version)
    {
        var existingProduct = await services.DbContext.Products.FindAsync(productId);
        if (existingProduct == null)
        {
            return TypedResults.NotFound();
        }

        var history = await services.DbContext.ProductHistories
            .Where(h => h.ProductId == productId && h.Version == version)
            .SingleOrDefaultAsync(services.CancellationToken);

        if (history == null)
        {
            return TypedResults.NotFound();
        }

        // Deserialize the historical product data
        var historicalData = JsonSerializer.Deserialize<JsonElement>(history.ProductData);

        // Validate that required properties exist before accessing them
        if (!historicalData.TryGetProperty("Name", out var nameElement) ||
            !historicalData.TryGetProperty("UrlSlug", out var urlSlugElement) ||
            !historicalData.TryGetProperty("Description", out var descriptionElement) ||
            !historicalData.TryGetProperty("BrandId", out var brandIdElement) ||
            !historicalData.TryGetProperty("CategoryId", out var categoryIdElement) ||
            !historicalData.TryGetProperty("IsActive", out var isActiveElement) ||
            !historicalData.TryGetProperty("IsDeleted", out var isDeletedElement))
        {
            return TypedResults.BadRequest("Historical product data is corrupted or missing required fields.");
        }

        // Save current product data to history before reverting
        var currentData = new
        {
            existingProduct.Id,
            existingProduct.Name,
            existingProduct.UrlSlug,
            existingProduct.Description,
            existingProduct.BrandId,
            existingProduct.CategoryId,
            existingProduct.CreatedAt,
            existingProduct.UpdatedAt,
            existingProduct.IsActive,
            existingProduct.IsDeleted,
            existingProduct.Version
        };

        var currentHistory = new ProductHistory
        {
            Id = Guid.CreateVersion7(),
            ProductId = existingProduct.Id,
            Version = existingProduct.Version,
            ProductData = JsonSerializer.Serialize(currentData),
            CreatedAt = DateTime.UtcNow
        };

        await services.DbContext.ProductHistories.AddAsync(currentHistory, services.CancellationToken);

        // Revert the product fields from historical data (safe access)
        existingProduct.Name = nameElement.GetString() ?? existingProduct.Name;
        existingProduct.UrlSlug = urlSlugElement.GetString() ?? existingProduct.UrlSlug;
        existingProduct.Description = descriptionElement.GetString() ?? existingProduct.Description;
        existingProduct.BrandId = brandIdElement.GetGuid();
        existingProduct.CategoryId = categoryIdElement.GetGuid();
        existingProduct.IsActive = isActiveElement.GetBoolean();
        existingProduct.IsDeleted = isDeletedElement.GetBoolean();
        existingProduct.UpdatedAt = DateTime.UtcNow;
        existingProduct.Version++;

        services.DbContext.Products.Update(existingProduct);
        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok(existingProduct);
    }
    #endregion

    private static async Task<Results<Ok, BadRequest, BadRequest<string>>> AddProductDimension([AsParameters] ApiServices services, Guid productId, Dimension[] dimensions)
    {
        if (dimensions == null || dimensions.Length == 0)
        {
            return TypedResults.BadRequest();
        }

        if (productId == Guid.Empty)
            return TypedResults.BadRequest("Product Id is required.");

        foreach (var dimension in dimensions)
        {
            if (string.IsNullOrEmpty(dimension.Id))
                return TypedResults.BadRequest("Dimension Id is required.");
        }

        var dimensionIds = dimensions.Select(d => d.Id).ToList();
        var existingDimensionIds = await services.DbContext.ProductDimensions
            .Where(pd => pd.ProductId == productId && dimensionIds.Contains(pd.DimensionId))
            .Select(pd => pd.DimensionId)
            .ToHashSetAsync(services.CancellationToken);

        var newProductDimensions = dimensions
            .Where(d => !existingDimensionIds.Contains(d.Id))
            .Select(d => new ProductDimension
            {
                ProductId = productId,
                DimensionId = d.Id
            })
            .ToList();

        if (newProductDimensions.Count > 0)
        {
            await services.DbContext.ProductDimensions.AddRangeAsync(newProductDimensions, services.CancellationToken);
        }

        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok();
    }
}
