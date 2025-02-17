using Microsoft.EntityFrameworkCore;

namespace ProductCatalog.Infrastructure.Entities;

[Index(nameof(Price))]
[Index(nameof(AvailableStock))]
[Index(nameof(IsPublished), nameof(IsDeleted), IsDescending = [true, false])]
public class Variant
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = default!;
    public string Name { get; set; } = default!;
    public double Price { get; set; }
    public int AvailableStock { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDeleted { get; set; }
    public Product Product { get; set; } = default!;
    public ICollection<VariantDimentionValue> VariantDimentionValues { get; set; } = [];
}
