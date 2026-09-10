using EventBus.Events;
using System.Text.Json.Serialization;

namespace ProductCatalog.Events;
public class VariantCreatedEvent: IntegrationEvent
{
    public Guid VariantId { get; set; }
    public Guid ProductId { get; set; }
    public VariantInfo Variant { get; set; } = default!;

    [JsonIgnore]
    public override string PartitionKey => ProductId.ToString();
}
