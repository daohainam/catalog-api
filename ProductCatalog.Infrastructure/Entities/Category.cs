using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ProductCatalog.Infrastructure.Entities;

[Index(nameof(TenantId))]
public class Category : ITenancyEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public Guid? ParentId { get; set; }
    public Category? Parent { get; set; } = default;
    public ICollection<Product> Products { get; set; } = [];
    public ICollection<Category> Children { get; set; } = [];
}
