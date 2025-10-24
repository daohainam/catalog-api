using System.Text.Json.Serialization;

namespace ProductCatalog.Infrastructure.Entity;
public class Group
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    [JsonIgnore]
    public List<Product> Products { get; set; } = [];
}
