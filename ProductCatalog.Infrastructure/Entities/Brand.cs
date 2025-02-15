using Microsoft.EntityFrameworkCore;

namespace ProductCatalog.Infrastructure.Entities
{
    [Index(nameof(TenantId))]
    public class Brand : ITenancyEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
        public ICollection<Product> Products { get; set; } = [];
    }
}
