namespace ProductCatalog.Api.Apis;

public static class CatalogApi
{
    private static readonly string InvalidDisplayType = $"Invalid display type. Valid types are: {string.Join(", ", DimensionDisplayTypes.All)}";
    private const int defaultPageSize = 10;
    private const int maxPageSize = 100;
    private const int maxNameLength = 200;
    private const int maxDescriptionLength = 4000;

    private static (int offset, int limit) ValidatePagination(int? offset, int? limit)
    {
        var validatedOffset = Math.Max(offset ?? 0, 0);
        var validatedLimit = Math.Min(Math.Max(limit ?? defaultPageSize, 1), maxPageSize);
        return (validatedOffset, validatedLimit);
    }

    public static IEndpointRouteBuilder MapCatalogApi(this IEndpointRouteBuilder builder)
    {
        // AddApiVersioning was registered but no endpoint ever declared a version,
        // so the "/api/v1" prefix was just a string. Binding the group to a real
        // version set makes ReportApiVersions and the X-Version header work, and
        // still resolves to /api/v1.
        var versionSet = builder.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .ReportApiVersions()
            .Build();

        builder.MapGroup("/api/v{version:apiVersion}")
              .MapCatalogApi()
              .WithTags("Product Catalog Api")
              .WithApiVersionSet(versionSet)
              .MapToApiVersion(new ApiVersion(1, 0))
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

        productApiGroup.MapGet("/{productId:guid}/history", GetProductHistory)
            .WithSummary("Lists the product's version history, newest first.");
        productApiGroup.MapGet("/{productId:guid}/history/{version:long}", GetProductHistoryByVersion)
            .WithSummary("Returns the stored snapshot for one product version.");
        productApiGroup.MapPost("/{productId:guid}/history/{version:long}/revert", RevertProduct)
            .WithSummary("Reverts the product's own fields to a previous version.")
            .WithDescription(
                "Only the product's own columns are versioned (name, slug, description, brand, " +
                "category, active and deleted flags). Variants, dimensions, images and group " +
                "membership are not captured in history and are left untouched by a revert. " +
                "The revert rolls forward: the restored state is saved as a new version rather " +
                "than rewinding the version number.");
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

        if (ValidateBrand(brand) is string brandError)
        {
            return TypedResults.BadRequest(brandError);
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

        if (ValidateBrand(brand) is string brandError)
        {
            return TypedResults.BadRequest(brandError);
        }

        if (brand.Id == Guid.Empty)
            brand.Id = Guid.CreateVersion7();

        await services.DbContext.Brands.AddAsync(brand);
        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok(brand);
    }

    private static string? ValidateBrand(Brand brand)
    {
        if (string.IsNullOrWhiteSpace(brand.Name))
            return "Brand Name is required and cannot be empty.";

        if (string.IsNullOrWhiteSpace(brand.UrlSlug))
            return "Brand UrlSlug is required and cannot be empty.";

        if (brand.Name.Length > maxNameLength)
            return $"Brand Name cannot exceed {maxNameLength} characters.";

        if (brand.UrlSlug.Length > maxNameLength)
            return $"Brand UrlSlug cannot exceed {maxNameLength} characters.";

        return null;
    }
    #endregion

    #region Variants
    private static async Task<Results<Ok<Variant>, BadRequest, NotFound>> UpdateVariant([AsParameters] ApiServices services, Guid productId, Guid variantId, Variant variant)
    {
        if (variant == null || variant.Id != variantId)
        {
            return TypedResults.BadRequest();
        }

        var product = await services.LoadProductForEventAsync(productId);
        var existingVariant = product?.Variants.Find(v => v.Id == variantId);
        if (product == null || existingVariant == null)
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

        // A variant change changes the product document on the read side.
        await QueueProductUpdatedEventAsync(services, product);

        await services.DbContext.SaveChangesAsync(services.CancellationToken);
        return TypedResults.Ok(existingVariant);
    }

    private static async Task<Results<Ok<Variant[]>, BadRequest, BadRequest<string>, NotFound>> CreateVariant([AsParameters] ApiServices services, Guid productId, Variant[] variants)
    {
        if (variants == null || variants.Length == 0)
        {
            return TypedResults.BadRequest();
        }

        var product = await services.LoadProductForEventAsync(productId);
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

        // Build the event from the in-memory graph: the new variants are not in the
        // database yet, and the event must commit in the same transaction.
        product.Variants.AddRange(variants.Where(v => !product.Variants.Contains(v)));
        await QueueProductUpdatedEventAsync(services, product);

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

            var product = await services.LoadProductForEventAsync(productId);
            if (product != null)
            {
                product.Dimensions.AddRange(newProductDimensions);
                await QueueProductUpdatedEventAsync(services, product);
            }
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

        if (ValidateProduct(product) is string productError)
        {
            return TypedResults.BadRequest(productError);
        }

        if (!await services.DbContext.Brands.AnyAsync(b => b.Id == product.BrandId, services.CancellationToken))
        {
            return TypedResults.BadRequest($"Brand '{product.BrandId}' does not exist.");
        }

        if (!await services.DbContext.Categories.AnyAsync(c => c.Id == product.CategoryId, services.CancellationToken))
        {
            return TypedResults.BadRequest($"Category '{product.CategoryId}' does not exist.");
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

        // Queued in the same SaveChanges as the product, so the event and the data
        // commit together.
        await services.AddOutboxMessageAsync(await product.ToProductCreatedEvent(services));

        await services.DbContext.Products.AddAsync(product);
        await services.DbContext.SaveChangesAsync(services.CancellationToken);

        return TypedResults.Ok(product);
    }

    private static string? ValidateProduct(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.Name))
            return "Product Name is required and cannot be empty.";

        if (product.Name.Length > maxNameLength)
            return $"Product Name cannot exceed {maxNameLength} characters.";

        if (product.UrlSlug is { Length: > maxNameLength })
            return $"Product UrlSlug cannot exceed {maxNameLength} characters.";

        if (product.Description is { Length: > maxDescriptionLength })
            return $"Product Description cannot exceed {maxDescriptionLength} characters.";

        if (product.BrandId == Guid.Empty)
            return "Product BrandId is required.";

        if (product.CategoryId == Guid.Empty)
            return "Product CategoryId is required.";

        return null;
    }

    /// <summary>
    /// Queues the product's post-change state so the read side stays in sync.
    /// Every write path that changes a product must call this before its
    /// SaveChanges - for a long time only CreateProduct emitted anything, so any
    /// update left Elasticsearch serving stale data.
    /// </summary>
    private static async Task QueueProductUpdatedEventAsync(ApiServices services, Product product)
    {
        await services.AddOutboxMessageAsync(await product.ToProductUpdatedEvent(services));
    }

    private static async Task<Results<NotFound, Ok, BadRequest<string>, Conflict<string>>> UpdateProduct([AsParameters] ApiServices services, Guid productId, Product product)
    {
        if (product == null)
        {
            return TypedResults.BadRequest("Product object is required.");
        }

        if (product.Id != Guid.Empty && product.Id != productId)
        {
            return TypedResults.BadRequest("Product Id in the body does not match the route.");
        }

        if (ValidateProduct(product) is string productError)
        {
            return TypedResults.BadRequest(productError);
        }

        if (!await services.DbContext.Categories.AnyAsync(c => c.Id == product.CategoryId, services.CancellationToken))
        {
            return TypedResults.BadRequest($"Category '{product.CategoryId}' does not exist.");
        }

        // Loaded with its collections so the outbox event carries the full product.
        var existingProduct = await services.LoadProductForEventAsync(productId);
        if (existingProduct == null)
        {
            return TypedResults.NotFound();
        }

        await AddHistoryEntryAsync(services, existingProduct);

        existingProduct.Name = product.Name;
        existingProduct.Description = product.Description;
        existingProduct.IsActive = product.IsActive;
        existingProduct.UpdatedAt = DateTime.UtcNow;
        existingProduct.CategoryId = product.CategoryId;
        existingProduct.Version++;

        await QueueProductUpdatedEventAsync(services, existingProduct);

        return await SaveProductChangeAsync(services, () => TypedResults.Ok());
    }

    /// <summary>
    /// Snapshots the product as it stands into history, at its current version.
    /// </summary>
    private static Task AddHistoryEntryAsync(ApiServices services, Product product)
    {
        var history = new ProductHistory
        {
            Id = Guid.CreateVersion7(),
            ProductId = product.Id,
            Version = product.Version,
            ProductData = JsonSerializer.Serialize(ProductSnapshot.From(product)),
            CreatedAt = DateTime.UtcNow
        };

        return services.DbContext.ProductHistories.AddAsync(history, services.CancellationToken).AsTask();
    }

    /// <summary>
    /// Saves a product change, translating a lost concurrency race into 409 rather
    /// than a 500. Two concurrent updates otherwise both write history at the same
    /// version and collide on IX_ProductHistories_ProductId_Version.
    /// </summary>
    private static async Task<Results<NotFound, Ok, BadRequest<string>, Conflict<string>>> SaveProductChangeAsync(
        ApiServices services, Func<Results<NotFound, Ok, BadRequest<string>, Conflict<string>>> onSuccess)
    {
        try
        {
            await services.DbContext.SaveChangesAsync(services.CancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Conflict("The product was modified by another request. Reload it and try again.");
        }

        return onSuccess();
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

    private static async Task<Results<Ok<Product>, NotFound, BadRequest<string>, Conflict<string>>> RevertProduct(
        [AsParameters] ApiServices services, Guid productId, long version)
    {
        // Loaded with its collections so the outbox event carries the full product.
        var existingProduct = await services.LoadProductForEventAsync(productId);
        if (existingProduct == null)
        {
            return TypedResults.NotFound();
        }

        var history = await services.DbContext.ProductHistories
            .AsNoTracking()
            .Where(h => h.ProductId == productId && h.Version == version)
            .SingleOrDefaultAsync(services.CancellationToken);

        if (history == null)
        {
            return TypedResults.NotFound();
        }

        var snapshot = ProductSnapshot.TryParse(history.ProductData);
        if (snapshot == null)
        {
            return TypedResults.BadRequest($"History record for version {version} cannot be read and cannot be reverted to.");
        }

        // Save current product data to history before reverting
        await AddHistoryEntryAsync(services, existingProduct);

        // Reverting rolls forward: the restored state becomes a new version so the
        // history stays append-only.
        existingProduct.Name = snapshot.Name!;
        existingProduct.UrlSlug = snapshot.UrlSlug!;
        existingProduct.Description = snapshot.Description!;
        existingProduct.BrandId = snapshot.BrandId;
        existingProduct.CategoryId = snapshot.CategoryId;
        existingProduct.IsActive = snapshot.IsActive;
        existingProduct.IsDeleted = snapshot.IsDeleted;
        existingProduct.UpdatedAt = DateTime.UtcNow;
        existingProduct.Version++;

        await QueueProductUpdatedEventAsync(services, existingProduct);

        try
        {
            await services.DbContext.SaveChangesAsync(services.CancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Conflict("The product was modified by another request. Reload it and try again.");
        }

        return TypedResults.Ok(existingProduct);
    }
    #endregion
}
