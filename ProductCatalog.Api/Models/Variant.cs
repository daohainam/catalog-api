namespace ProductCatalog.Api.Models;

public record VariantCreate(Guid ProductId, string Sku, string Name, double Price, int AvailableStock, DimensionValue[] DimensionValues);
public record Variant(Guid Id, Guid ProductId, string Sku, string Name, double Price, int AvailableStock, DimensionValue[] DimensionValues) : VariantCreate(ProductId, Sku, Name, Price, AvailableStock, DimensionValues);
