namespace ProductCatalog.Api.Models
{
    public class Variant
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string Sku { get; set; } = default!;
        public string Name { get; set; } = default!;
        public double Price { get; set; }
        public int AvailableStock { get; set; }
    }
}
