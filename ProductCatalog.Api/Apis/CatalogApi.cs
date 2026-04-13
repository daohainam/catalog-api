namespace ProductCatalog.Api.Apis;

public static class CatalogApi
{
    private static readonly string InvalidDisplayType = $"Invalid display type. Valid types are: {string.Join(", ", DimensionDisplayTypes.All)}";
    private const int defaultPageSize = 10;
    private const int maxPageSize = 100;

    private static (int offset, int limit) ValidatePagination(int? offset, int? limit)
    {
        var validatedOffset = Math.Max(offset ?? 0, 0);
        var validatedLimit = Math.Min(Math.Max(limit ?? defaultPageSize, 1), maxPageSize);
        return (validatedOffset, validatedLimit);
    }

    public static IEndpointRouteBuilder MapCatalogApi(this IEndpointRouteBuilder builder)
    {
        builder.MapGroup("/api/v1")
              .MapCatalogApi()
              .WithTags("Product Catalog Api")
              .RequireRateLimiting("fixed");

        return builder;
    }

    public static RouteGroupBuilder MapCatalogApi(this RouteGroupBuilder group)
    {
        var categoryApiGroup = group.MapGroup("categories").WithTags("Category");
        categoryApiGroup.MapPost("/", CreateCategory);

        var brandApiGroup = group.MapGroup("brands").WithTags("Brand");
        brandApiGroup.MapPost("/", CreateBrand);
        brandApiGroup.MapGet("/", FindBrands);
        brandApiGroup.MapGet("/{brandId:guid}", FindBrandById);
        brandApiGroup.MapPut("/{brandId:guid}", UpdateBrand);

        var groupApiGroup = group.MapGroup("groups").WithTags("Group");
        groupApiGroup.MapPost("/", CreateGroup);

        var dimensionApiGroup = group.MapGroup("dimensions").WithTags("Dimension");
        dimensionApiGroup.MapPost("/", CreateDimentions);
        dimensionApiGroup.MapPost("/{id}/values", AddDimentionValues);
        dimensionApiGroup.MapGet("/", async ([AsParameters] ApiServices services, [FromQuery] int? offset = 0, [FromQuery] int? limit = defaultPageSize) =>
        {
            var (validatedOffset, validatedLimit) = ValidatePagination(offset, limit);
            return await services.DbContext.Dimensions
            .AsNoTracking()
            .Include(d => d.Values)
            .OrderBy(d => d.Id)
            .Skip(validatedOffset).Take(validatedLimit)
            .ToListAsync(services.CancellationToken);
        });

        #region Products and Variants

        var productApiGroup = group.MapGroup("products").WithTags("Product");

        productApiGroup.MapPost("/", CreateProduct);
        productApiGroup.MapPut("/{productId:guid}", UpdateProduct);
        productApiGroup.MapGet("/", async ([AsParameters] ApiServices services, [FromQuery] int? offset = 0, [FromQuery] int? limit = defaultPageSize) =>
        {
            var (validatedOffset, validatedLimit) = ValidatePagination(offset, limit);
            return await services.DbContext.Products
            .AsNoTracking()
            // .Where(p => !p.IsDeleted) // in this in internal API, we return all products except deleted ones
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

        productApiGroup.MapGet("/{productId:guid}/variants", FindVariants);
        productApiGroup.MapPost("/{productId:guid}/variants", CreateVariant);
        productApiGroup.MapPut("/{productId:guid}/variants/{variantId:guid}", UpdateVariant);

        productApiGroup.MapGet("/{productId:guid}/history", GetProductHistory);
        productApiGroup.MapGet("/{productId:guid}/history/{version:long}", GetProductHistoryByVersion);
        productApiGroup.MapPost("/{productId:guid}/history/{version:long}/revert", RevertProduct);
        #endregion

        return group;
    }

    #region Brands
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
            return TypedResults.BadRequest("Category Name is required.");
        }

        if (string.IsNullOrEmpty(brand.UrlSlug))
        {
            return TypedResults.BadRequest("Category UrlSlug is required.");
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

    private static async Task<Results<Ok<Brand[]>, NotFound>> FindBrands([AsParameters] ApiServices services, [FromQuery] int? offset = 0, [FromQuery] int? limit = defaultPageSize)
    {
        var (validatedOffset, validatedLimit) = ValidatePagination(offset, limit);
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
    #endregion

    #region Variants
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

    private static async Task<Results<Ok<Group>, BadRequest, BadRequest<string>>> CreateGroup([AsParameters] ApiServices services, Group group)
    {
        if (group == null)
        {
            return TypedResults.BadRequest();
        }
        if (string.IsNullOrEmpty(group.Name))
        {
            return TypedResults.BadRequest("Group Name is required.");
        }

        if (group.Id == Guid.Empty)
            group.Id = Guid.CreateVersion7();

        await services.DbContext.Groups.AddAsync(group);
        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok(group);
    }

    private static async Task<Results<Ok<Category>, BadRequest, BadRequest<string>>> CreateCategory([AsParameters] ApiServices services, Category category)
    {
        if (category == null)
        {
            return TypedResults.BadRequest();
        }

        if (string.IsNullOrEmpty(category.Name))
        {
            return TypedResults.BadRequest("Category Name is required.");
        }

        if (string.IsNullOrEmpty(category.UrlSlug))
        {
            return TypedResults.BadRequest("Category UrlSlug is required.");
        }

        if (category.Id == Guid.Empty)
            category.Id = Guid.CreateVersion7();

        await services.DbContext.Categories.AddAsync(category);
        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok(category);
    }

    private static async Task<Results<Ok<DimensionValue[]>, BadRequest, BadRequest<string>>> AddDimentionValues([AsParameters] ApiServices services, [FromRoute] string id, DimensionValue[] dimensionValues)
    {
        if (dimensionValues == null || dimensionValues.Length == 0)
        {
            return TypedResults.BadRequest();
        }

        foreach (var dimensionValue in dimensionValues)
        {
            if (dimensionValue.Id == Guid.Empty)
                dimensionValue.Id = Guid.CreateVersion7();

            dimensionValue.DimensionId = id;
        }
        await services.DbContext.DimensionValues.AddRangeAsync(dimensionValues, services.CancellationToken);
        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok(dimensionValues);
    }

    private static async Task<Results<Ok<Dimension[]>, BadRequest, BadRequest<string>>> CreateDimentions([AsParameters] ApiServices services, Dimension[] dimensions)
    {
        if (dimensions == null || dimensions.Length == 0)
        {
            return TypedResults.BadRequest();
        }

        foreach (var dimension in dimensions)
        {
            if (string.IsNullOrEmpty(dimension.Id))
                return TypedResults.BadRequest("Dimension Id is required.");
            if (string.IsNullOrEmpty(dimension.DisplayType))
                dimension.DisplayType = DimensionDisplayTypes.Text;
            else if (!DimensionDisplayTypes.Has(dimension.DisplayType))
                return TypedResults.BadRequest(InvalidDisplayType);

            if (!IsValidDimensionId(dimension.Id))
            {
                return TypedResults.BadRequest("Dimension Id can only contain alphanumeric characters and underscores.");
            }

            dimension.DefaultValue ??= "";
            await services.DbContext.Dimensions.AddAsync(dimension);

            if (dimension.Values != null && dimension.Values.Count > 0)
            {
                foreach (var value in dimension.Values)
                {
                    if (value.Id == Guid.Empty)
                        value.Id = Guid.CreateVersion7();
                    value.DimensionId = dimension.Id;
                }
                await services.DbContext.DimensionValues.AddRangeAsync(dimension.Values);
            }
        }

        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok(dimensions);
    }

    private static bool IsValidDimensionId(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        if (!(id[0] >= 'a' && id[0] <= 'z')) // must start with a lowercase letter
        {
            return false;
        }

        return id.All(c => c == '_' || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')); // only allow lowercase letters, numbers and underscores
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
                    //await services.DbContext.VariantDimensionValues.AddRangeAsync(variant.DimensionValues);
                }
            }
        }

        // create outbox event

        var evt = await product.ToProductCreatedEvent(services);

        await services.DbContext.AddAsync(new LogTailingOutboxMessage()
        {
            Id = Guid.NewGuid(),
            CreationDate = DateTime.UtcNow,
            PayloadType = typeof(ProductCatalog.Events.ProductCreatedEvent).FullName ?? throw new Exception($"Could not get fullname of type {evt.GetType()}"),
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
        [AsParameters] ApiServices services, Guid productId, [FromQuery] int? offset = 0, [FromQuery] int? limit = defaultPageSize)
    {
        var productExists = await services.DbContext.Products.AnyAsync(p => p.Id == productId, services.CancellationToken);
        if (!productExists)
        {
            return TypedResults.NotFound();
        }

        var (validatedOffset, validatedLimit) = ValidatePagination(offset, limit);

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

        // Revert the product fields from historical data
        existingProduct.Name = historicalData.GetProperty("Name").GetString()!;
        existingProduct.UrlSlug = historicalData.GetProperty("UrlSlug").GetString()!;
        existingProduct.Description = historicalData.GetProperty("Description").GetString()!;
        existingProduct.BrandId = historicalData.GetProperty("BrandId").GetGuid();
        existingProduct.CategoryId = historicalData.GetProperty("CategoryId").GetGuid();
        existingProduct.IsActive = historicalData.GetProperty("IsActive").GetBoolean();
        existingProduct.IsDeleted = historicalData.GetProperty("IsDeleted").GetBoolean();
        existingProduct.UpdatedAt = DateTime.UtcNow;
        existingProduct.Version++;

        services.DbContext.Products.Update(existingProduct);
        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok(existingProduct);
    }
    #endregion
}
