namespace ProductCatalog.Infrastructure.Entities
{
    public class DimensionValue
    {
        public Guid Id { get; set; }
        public Guid DimensionId { get; set; }
        public Dimension Dimension { get; set; } = default!;
        public string Value { get; set; } = default!;
    }
}
