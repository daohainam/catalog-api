using EventBus.Events;

namespace EventBus.Kafka;
public class KafkaEventPublisher(string topic, IProducer<string, MessageEnvelop> producer, ILogger logger) : IEventPublisher
{
    public async Task<bool> PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IntegrationEvent
    {
        var json = JsonSerializer.Serialize(@event, @event.GetType());
        logger.LogInformation("Publishing event {EventType} to topic {Topic}", @event.GetType().Name, topic);

        try
        {
            await producer.ProduceAsync(topic, new Message<string, MessageEnvelop>
                {
                    // Keyed by aggregate id so events for one product stay ordered
                    // while different products spread across partitions.
                    Key = @event.PartitionKey,
                    Value = new MessageEnvelop(@event.GetType(), json)
                },
                cancellationToken
            );

            logger.LogInformation("Published event {EventId}", @event.EventId);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing event {EventId}", @event.EventId);

            return false;
        }
    }
}
