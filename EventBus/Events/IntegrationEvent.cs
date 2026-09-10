using System.Text.Json.Serialization;

namespace EventBus.Events;
public class IntegrationEvent
{
    // These use init accessors (not private set) so System.Text.Json can restore
    // them when the event is deserialized on the consumer side. With a private
    // setter the values were silently regenerated on every deserialization,
    // which made EventId useless for correlation and deduplication.
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public DateTime EventCreationDate { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Kafka partition key. Override with the aggregate id so that events for one
    /// aggregate keep their relative order while different aggregates spread
    /// across partitions. Keying by event type would put every event on a single
    /// partition and prevent consumers from scaling out.
    /// </summary>
    [JsonIgnore]
    public virtual string PartitionKey => EventId.ToString();
}
