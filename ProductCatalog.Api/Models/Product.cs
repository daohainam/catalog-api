using Microsoft.EntityFrameworkCore;

namespace ProductCatalog.Api.Models
{
    public class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
        public double PriceFrom { get; set; }
        public double PriceTo { get; set; }
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = default!;
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = default!;
        public string[] Images { get; set; } = [];
    }
}
