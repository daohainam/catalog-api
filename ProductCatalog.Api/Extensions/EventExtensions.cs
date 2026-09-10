using EventBus.Events;
using ProductCatalog.Events;

namespace ProductCatalog.Api.Extensions;
public static class EventExtensions
{
    /// <summary>
    /// Loads a product with everything the event payload needs, tracked so the
    /// caller can mutate it and build the event from the same in-memory instance.
    /// Handlers that fetch a product with FindAsync get only the scalar columns,
    /// and an event built from that would publish a product with no variants or
    /// images. Building the event before SaveChanges also keeps the event and the
    /// data change in one transaction.
    /// </summary>
    public static Task<Product?> LoadProductForEventAsync(this ApiServices services, Guid productId)
    {
        return services.DbContext.Products
            .Include(p => p.Variants).ThenInclude(v => v.DimensionValues)
            .Include(p => p.Dimensions)
            .Include(p => p.Groups)
            .Include(p => p.Images).ThenInclude(i => i.Image)
            .SingleOrDefaultAsync(p => p.Id == productId, services.CancellationToken);
    }

    /// <summary>
    /// Queues an integration event in the outbox table. It is not saved here: the
    /// caller's SaveChangesAsync commits the event and the business data in one
    /// transaction, which is the whole point of the outbox.
    /// </summary>
    public static async Task AddOutboxMessageAsync(this ApiServices services, IntegrationEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var payloadType = @event.GetType().FullName
            ?? throw new InvalidOperationException($"Could not get the full name of type {@event.GetType()}");

        await services.DbContext.LogTailingOutboxMessages.AddAsync(new LogTailingOutboxMessage
        {
            Id = Guid.CreateVersion7(),
            CreationDate = DateTime.UtcNow,
            PayloadType = payloadType,
            Payload = JsonSerializer.Serialize(@event, @event.GetType()),
        }, services.CancellationToken);
    }

    public async static Task<ProductCreatedEvent> ToProductCreatedEvent(this Product product, ApiServices services)
    {
        ArgumentNullException.ThrowIfNull(product);

        return new ProductCreatedEvent
        {
            ProductId = product.Id,
            Product = await product.ToProductInfo(services)
        };
    }

    public async static Task<ProductUpdatedEvent> ToProductUpdatedEvent(this Product product, ApiServices services)
    {
        ArgumentNullException.ThrowIfNull(product);

        return new ProductUpdatedEvent
        {
            ProductId = product.Id,
            Product = await product.ToProductInfo(services)
        };
    }

    public async static Task<ProductInfo> ToProductInfo(this Product product, ApiServices services)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(services);

        var brand = await services.DbContext.Brands
            .AsNoTracking()
            .Where(b => b.Id == product.BrandId)
            .SingleOrDefaultAsync(services.CancellationToken);

        if (brand == null)
        {
            throw new InvalidOperationException($"Brand with ID {product.BrandId} not found");
        }

        var category = await services.DbContext.Categories
            .AsNoTracking()
            .Where(c => c.Id == product.CategoryId)
            .SingleOrDefaultAsync(services.CancellationToken);

        if (category == null)
        {
            throw new InvalidOperationException($"Category with ID {product.CategoryId} not found");
        }

        // Navigation collections are list-initialized on Product, but an explicit
        // null in the request body overwrites those initializers, so guard each one.
        var productDimensions = product.Dimensions ?? [];
        var productVariants = product.Variants ?? [];
        var productGroups = product.Groups ?? [];
        var productImages = product.Images ?? [];

        var dimensionIds = productDimensions.Select(d => d.DimensionId).ToList();
        var dimensions = await services.DbContext.Dimensions
            .AsNoTracking()
            .Where(d => dimensionIds.Contains(d.Id))
            .Include(d => d.Values)
            .ToListAsync(services.CancellationToken);

        return new ProductInfo
        {
            Name = product.Name,
            UrlSlug = product.UrlSlug,
            Description = product.Description,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            IsActive = product.IsActive,

            Path = [
                new() {
                    CategoryId = category.Id,
                    Name = category.Name ?? string.Empty,
                    UrlSlug = category.UrlSlug ?? string.Empty,
                    Description = category.Description ?? string.Empty
                }
            ],
            Brand = new BrandInfo
            {
                BrandId = brand.Id,
                Name = brand.Name ?? string.Empty,
                Description = brand.Description ?? string.Empty,
                LogoUrl = brand.LogoUrl ?? string.Empty
            },
            Variants = [.. productVariants.Select(v => new VariantInfo
            {
                Id = v.Id,
                ProductId = v.ProductId,
                Sku = v.Sku,
                BarCode = v.BarCode,
                Price = v.Price,
                Description = v.Description,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt,
                IsActive = v.IsActive,
                DimensionValues = [.. (v.DimensionValues ?? []).Select(dv => new VariantDimensionValueInfo()
                {
                    DimensionId = dv.DimensionId,
                    Value = dv.Value
                })]
            })],
            Dimensions = [.. dimensions.Select(d => new DimensionInfo
            {
                DimensionId = d.Id,
                Name = d.Name,
                DisplayType = d.DisplayType,
                Values = [.. (d.Values ?? []).Select(v => new DimensionValueInfo
                {
                    Value = v.Value,
                    DisplayValue = v.DisplayValue ?? string.Empty
                })]
            })],
            Groups = [.. productGroups.Select(g => new GroupInfo
            {
                GroupId = g.Id,
                Name = g.Name
            })],
            Images = [.. productImages.Select(i => new ProductImageInfo
            {
                ImageId = i.Id,
                ProductId = i.ProductId,
                AltText = i.AltText,
                SortOrder = i.SortOrder,
                ImageUrl = i.Image != null && !string.IsNullOrEmpty(i.Image.BaseUrl)
                    ? new Uri(new Uri(i.Image.BaseUrl), i.Image.FileName).ToString()
                    : i.Image?.FileName ?? string.Empty
            })]
        };
    }
}
