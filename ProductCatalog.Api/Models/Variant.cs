namespace ProductCatalog.Api.Models;

public record VariantCreate(Guid ProductId, string Sku, string Name, double Price, int AvailableStock);
public record Variant(Guid Id, Guid ProductId, string Sku, string Name, double Price, int AvailableStock) : VariantCreate(ProductId, Sku, Name, Price, AvailableStock);
