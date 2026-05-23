namespace ProductCatalog.Infrastructure.Entity
{
    public class Variant
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string Sku { get; set; } = default!;
        public string BarCode { get; set; } = default!;
        public decimal Price { get; set; }
        public string Description { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public List<VariantDimensionValue> DimensionValues { get; set; } = [];
    }
}
