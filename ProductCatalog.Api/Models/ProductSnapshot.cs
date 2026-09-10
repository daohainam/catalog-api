namespace ProductCatalog.Api.Models;

/// <summary>
/// Shape of the JSON stored in <see cref="ProductHistory.ProductData"/>.
///
/// Reverting used to read the stored JSON with JsonElement.GetProperty, which
/// throws on a missing key or a null value - so any history row written under a
/// different shape produced a 500. Deserializing into this record instead lets a
/// revert reject a snapshot it cannot read and return 400.
///
/// SchemaVersion is absent (and therefore 0) on rows written before this record
/// existed; those rows use the same property names, so they still load.
/// </summary>
public sealed record ProductSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? UrlSlug { get; init; }
    public string? Description { get; init; }
    public Guid BrandId { get; init; }
    public Guid CategoryId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool IsActive { get; init; }
    public bool IsDeleted { get; init; }
    public long Version { get; init; }

    public static ProductSnapshot From(Product product) => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        Id = product.Id,
        Name = product.Name,
        UrlSlug = product.UrlSlug,
        Description = product.Description,
        BrandId = product.BrandId,
        CategoryId = product.CategoryId,
        CreatedAt = product.CreatedAt,
        UpdatedAt = product.UpdatedAt,
        IsActive = product.IsActive,
        IsDeleted = product.IsDeleted,
        Version = product.Version
    };

    /// <summary>
    /// Reads a stored snapshot, or returns null when the JSON is unreadable or is
    /// missing values the product columns require.
    /// </summary>
    public static ProductSnapshot? TryParse(string productData)
    {
        ProductSnapshot? snapshot;

        try
        {
            snapshot = JsonSerializer.Deserialize<ProductSnapshot>(productData);
        }
        catch (JsonException)
        {
            return null;
        }

        if (snapshot is null || snapshot.SchemaVersion > CurrentSchemaVersion)
        {
            return null;
        }

        if (snapshot.Name is null || snapshot.UrlSlug is null || snapshot.Description is null)
        {
            return null;
        }

        return snapshot;
    }
}
