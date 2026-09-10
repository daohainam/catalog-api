namespace EventBus.Abstractions;

/// <summary>
/// Destination for messages that could not be handled after all retries.
/// Publishing to it lets the consumer commit the offset and move on without
/// silently discarding the message.
/// </summary>
public interface IDeadLetterPublisher
{
    Task<bool> PublishAsync(MessageEnvelop message, string reason, CancellationToken cancellationToken = default);
}
