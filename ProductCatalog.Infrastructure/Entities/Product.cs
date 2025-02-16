using Microsoft.EntityFrameworkCore;

namespace ProductCatalog.Infrastructure.Entities
{
    [Index(nameof(TenantId))]
    public class Product : ITenancyEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
        public double PriceFrom { get; set; }
        public double PriceTo { get; set; }
        public bool IsPublished { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int VariantCount { get; set; }
        public Guid CategoryId { get; set; }
        public Guid BrandId { get; set; }
        public string[] Images { get; set; } = [];
        public Category Category { get; set; } = default!;
        public Brand Brand { get; set; } = default!;
        public ICollection<Variant> Variants { get; set; } = [];
        public ICollection<Dimension> Dimensions { get; set; } = [];
    }
}
