using System.ComponentModel.DataAnnotations;

namespace ProductCatalog.Api.Infrastructure.Domain
{
    public class Category
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
        public Guid ParentId { get; set; }
    }
}
