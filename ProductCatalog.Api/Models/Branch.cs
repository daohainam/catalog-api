using Microsoft.EntityFrameworkCore;

namespace ProductCatalog.Api.Models
{
    public class Branch
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
    }
}
