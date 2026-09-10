using ProductCatalog.Events;
using System.Globalization;
using System.Text;

namespace ProductCatalog.Search;

public static class ProductEsMapper
{
    public static ProductIndexDocument Map(ProductCreatedEvent e) => Map(e.ProductId, e.Product);

    public static ProductIndexDocument Map(ProductUpdatedEvent e) => Map(e.ProductId, e.Product);

    public static ProductIndexDocument Map(Guid productId, ProductInfo product)
    {
        // Collections are list-initialized on ProductInfo, but an explicit null in
        // the incoming JSON overwrites those initializers, so every access is guarded.
        var p = product ?? throw new ArgumentNullException(nameof(product));

        // Dimension metadata lookup
        var dimById = new Dictionary<string, DimensionInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in p.Dimensions ?? [])
            dimById[d.DimensionId] = d;

        var productVariants = p.Variants ?? [];
        var variants = new List<VariantDoc>(productVariants.Count);
        foreach (var v in productVariants)
        {
            var variantDimensionValues = v.DimensionValues ?? [];
            var dimsNested = new List<VariantDimensionDoc>(variantDimensionValues.Count);
            var dimsFlat = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var dv in variantDimensionValues)
            {
                // resolve name & display_value from metadata
                string dimId = dv.DimensionId;
                string name = dimById.TryGetValue(dimId, out var meta) ? meta.Name : dimId;
                string display = dv.Value;

                if (meta != null && meta.Values is { Count: > 0 })
                {
                    var match = meta.Values.Find(x => string.Equals(x.Value, dv.Value, StringComparison.OrdinalIgnoreCase));
                    if (match != null && !string.IsNullOrWhiteSpace(match.DisplayValue))
                        display = match.DisplayValue;
                }

                dimsNested.Add(new VariantDimensionDoc
                {
                    DimensionId = dimId,
                    Value = NormalizeKeyword(dv.Value),
                    DisplayValue = display
                });

                var key = NormalizeKeyword(name);
                var val = NormalizeKeyword(dv.Value);
                if (!dimsFlat.ContainsKey(key)) dimsFlat[key] = val;
            }

            variants.Add(new VariantDoc
            {
                VariantId = v.Id,
                Sku = v.Sku,
                BarCode = v.BarCode,
                Price = v.Price,
                InStock = v.InStock,
                IsActive = v.IsActive,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt,
                Description = v.Description ?? "",
                Dimensions = dimsNested,
                DimsFlat = dimsFlat
            });
        }

        // Rollups
        decimal? priceMin = variants.Count > 0 ? variants.Min(x => x.Price) : null;
        bool hasStock = variants.Exists(x => x.InStock);
        var primary = ChoosePrimary(variants);

        // Category leaf + breadcrumb from Product.Path
        Guid categoryId = Guid.Empty;
        string categoryName = "";
        string categorySlug = "";
        string categoryPath = "";
        if (p.Path is { Count: > 0 })
        {
            var leaf = p.Path[^1];
            categoryId = leaf.CategoryId;
            categoryName = leaf.Name;
            categorySlug = leaf.UrlSlug;
            categoryPath = string.Join("/", p.Path.Select(x => x.UrlSlug));
        }

        var brand = p.Brand;

        return new ProductIndexDocument
        {
            ProductId = productId,

            Name = p.Name,
            Slug = p.UrlSlug,
            Description = p.Description ?? "",

            BrandId = brand?.BrandId ?? Guid.Empty,
            BrandName = brand?.Name ?? "",

            CategoryId = categoryId,
            CategoryName = categoryName,
            CategorySlug = categorySlug,
            CategoryPath = categoryPath,

            Dimensions = [.. dimById.Values.Select(d => new DimensionDoc
            {
                DimensionId = d.DimensionId,
                Name = d.Name,
                DisplayType = d.DisplayType
            })],

            GroupIds = [.. (p.Groups ?? []).Select(g => g.GroupId)],
            GroupNames = [.. (p.Groups ?? []).Select(g => g.Name)],

            Images = [.. (p.Images ?? [])
                .OrderBy(i => i.SortOrder)
                .Select(i => new ImageDoc { Url = i.ImageUrl, Alt = i.AltText ?? "", SortOrder = i.SortOrder })],

            PriceMin = priceMin,
            InStock = hasStock,
            VariantCount = variants.Count,

            Variants = variants,
            PrimaryVariant = primary,

            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,

            Suggest = new SimpleCompletion { Input = [p.Name, brand?.Name ?? ""] }
        };
    }

    private static PrimaryVariantDoc? ChoosePrimary(List<VariantDoc> variants)
    {
        if (variants.Count == 0) return null;
        var best = variants.Where(v => v.InStock).OrderBy(v => v.Price).FirstOrDefault()
               ?? variants.OrderBy(v => v.Price).First();
        return new PrimaryVariantDoc { VariantId = best.VariantId, Price = best.Price, InStock = best.InStock };
    }

    private static string NormalizeKeyword(string s)
    {
        s ??= "";
        var lower = s.Trim().ToLowerInvariant();
        return RemoveDiacritics(lower);
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}