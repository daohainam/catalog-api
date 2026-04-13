namespace ProductCatalog.Infrastructure.Entity;

public class ProductHistory
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public long Version { get; set; }
    public string ProductData { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
