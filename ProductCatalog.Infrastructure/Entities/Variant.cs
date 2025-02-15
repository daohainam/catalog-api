using Microsoft.EntityFrameworkCore;

namespace ProductCatalog.Infrastructure.Entities
{
    [Index(nameof(Price))]
    [Index(nameof(AvailableStock))]
    public class Variant
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string Sku { get; set; } = default!;
        public string Name { get; set; } = default!;
        public double Price { get; set; }
        public int AvailableStock { get; set; }
        public Product Product { get; set; } = default!;
        public ICollection<VariantDimentionValue> VariantDimentionValues { get; set; } = [];
    }
}
