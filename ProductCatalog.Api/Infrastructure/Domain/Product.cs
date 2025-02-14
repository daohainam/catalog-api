using Microsoft.Extensions.Hosting;
using System.ComponentModel.DataAnnotations;

namespace ProductCatalog.Api.Infrastructure.Domain
{
    public class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
        public double PriceFrom { get; set; }
        public double PriceTo { get; set; }
        public Guid CategoryId { get; set; }
        public Guid BranchId { get; set; }
        public string[] Images { get; set; } = [];
        public ICollection<Variant> Variants { get; set; } = [];
    }
}
