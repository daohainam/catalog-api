namespace ProductCatalog.Api.Infrastructure.Domain
{
    public class Dimension
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string Name { get; set; } = default!;
        public string DisplayName { get; set; } = default!;
        public DimensionDisplayTypes DisplayType { get; set; }
        public ICollection<DimensionValue> DimensionValues { get; set; } = [];
    }

    public enum DimensionDisplayTypes
    {
        Color,
        Text
    }
}
