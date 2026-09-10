namespace EventBus.Kafka;

internal class KafkaDeadLetterPublisher(string topic, IProducer<string, MessageEnvelop> producer, ILogger logger) : IDeadLetterPublisher
{
    public async Task<bool> PublishAsync(MessageEnvelop message, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            var record = new Message<string, MessageEnvelop>
            {
                Key = message.MessageTypeName,
                Value = message,
                Headers = [new Header("dlq-reason", Encoding.UTF8.GetBytes(reason))]
            };

            await producer.ProduceAsync(topic, record, cancellationToken);

            logger.LogWarning("Dead-lettered a {MessageType} message to {Topic}: {Reason}",
                message.MessageTypeName, topic, reason);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to dead-letter a {MessageType} message to {Topic}",
                message.MessageTypeName, topic);

            return false;
        }
    }
}
