namespace ProductCatalog.Api.Infrastructure.Domain
{
    public class VariantDimentionValue
    {
        public Guid Id { get; set; }
        public Guid VariantId { get; set; }
        public Guid DimensionId { get; set; }
        public string Value { get; set; } = default!;
        public Variant Variant { get; set; } = default!;
    }
}
