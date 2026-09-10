using Confluent.Kafka;
using EventBus;
using EventBus.Abstractions;
using EventBus.Events;
using ProductCatalog.SearchSyncService.EventHandlers;

namespace ProductCatalog.SearchSyncService;

public class EventHandlingService(IConsumer<string, MessageEnvelop> consumer,
    EventHandlingWorkerOptions options,
    IIntegrationEventFactory integrationEventFactory,
    IDeadLetterPublisher deadLetterPublisher,
    IServiceScopeFactory serviceScopeFactory,
    ILoggerFactory loggerFactory) : BackgroundService
{
    private readonly ILogger logger = loggerFactory.CreateLogger(options.ServiceName);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Consume blocks the calling thread, so the loop runs on a dedicated task
        // instead of holding up host startup.
        return Task.Run(() => ConsumeLoopAsync(stoppingToken), stoppingToken);
    }

    private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Subscribing to topics [{Topics}]...", string.Join(',', options.Topics));

        try
        {
            consumer.Subscribe(options.Topics);

            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, MessageEnvelop>? consumeResult;

                try
                {
                    consumeResult = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    // Back off rather than spinning: a broker outage would otherwise
                    // turn this into a hot loop.
                    logger.LogError(ex, "Error consuming from Kafka. Retrying in {Delay}s.", options.ConsumeErrorDelay.TotalSeconds);
                    await Task.Delay(options.ConsumeErrorDelay, stoppingToken);
                    continue;
                }

                if (consumeResult is null || consumeResult.IsPartitionEOF)
                {
                    continue;
                }

                if (consumeResult.Message?.Value is null)
                {
                    logger.LogDebug("Skipping tombstone at {TopicPartitionOffset}", consumeResult.TopicPartitionOffset);
                    consumer.Commit(consumeResult);
                    continue;
                }

                using var scope = serviceScopeFactory.CreateScope();
                await ProcessMessageAsync(scope.ServiceProvider, consumeResult.Message.Value, stoppingToken);

                // Committed only once the message has been handled or dead-lettered.
                consumer.Commit(consumeResult);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("{ServiceName} is stopping", options.ServiceName);
        }
        finally
        {
            // Leaves the consumer group promptly instead of waiting out session.timeout.ms.
            consumer.Close();
        }
    }

    private async Task ProcessMessageAsync(IServiceProvider services, MessageEnvelop message, CancellationToken cancellationToken)
    {
        var messageTypeName = Sanitize(message.MessageTypeName);

        IntegrationEvent? evt;
        try
        {
            evt = integrationEventFactory.CreateEvent(message.MessageTypeName, message.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not deserialize a {MessageType} message", messageTypeName);
            await deadLetterPublisher.PublishAsync(message, $"Deserialization failed: {ex.Message}", cancellationToken);
            return;
        }

        if (evt is null)
        {
            logger.LogWarning("Unknown event type {MessageType}. Message dead-lettered.", messageTypeName);
            await deadLetterPublisher.PublishAsync(message, "Unknown event type", cancellationToken);
            return;
        }

        if (!options.AcceptEvent(evt))
        {
            logger.LogDebug("Event skipped: {MessageType}", messageTypeName);
            return;
        }

        logger.LogDebug("Processing message {MessageType} ({EventId})", messageTypeName, evt.EventId);

        var handler = services.GetRequiredService<IEventHandlerFactory>().CreateHandler(services, evt);
        if (handler is null)
        {
            logger.LogDebug("No handler registered for event type {MessageType}. Message skipped.", messageTypeName);
            return;
        }

        if (await TryHandleAsync(handler, evt, messageTypeName, cancellationToken))
        {
            return;
        }

        logger.LogError("Event {MessageType} ({EventId}) failed after {MaxAttempts} attempts. Dead-lettering.",
            messageTypeName, evt.EventId, options.MaxHandlerAttempts);
        await deadLetterPublisher.PublishAsync(message, $"Handler failed after {options.MaxHandlerAttempts} attempts", cancellationToken);
    }

    private async Task<bool> TryHandleAsync(IEventHandler handler, IntegrationEvent evt, string messageTypeName, CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= options.MaxHandlerAttempts; attempt++)
        {
            try
            {
                await handler.HandleAsync(evt, cancellationToken);
                logger.LogDebug("Handled {MessageType} on attempt {Attempt}", messageTypeName, attempt);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw; // Don't retry on shutdown.
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Attempt {Attempt}/{MaxAttempts} failed for {MessageType}",
                    attempt, options.MaxHandlerAttempts, messageTypeName);
            }

            if (attempt < options.MaxHandlerAttempts)
            {
                // Delay between attempts only - the first attempt runs immediately.
                var delay = TimeSpan.FromTicks(options.HandlerRetryDelay.Ticks * (1L << (attempt - 1)));
                await Task.Delay(delay, cancellationToken);
            }
        }

        return false;
    }

    // Message type names arrive off the wire, so strip line breaks before logging them.
    private static string Sanitize(string? value) =>
        value?.Replace("\r", "").Replace("\n", "") ?? "";
}

public class EventHandlingWorkerOptions
{
    public string KafkaGroupId { get; set; } = "event-handling";
    public List<string> Topics { get; set; } = [];
    public string DeadLetterTopic { get; set; } = "catalog-events-dlq";
    public IIntegrationEventFactory? IntegrationEventFactory { get; set; }
    public string ServiceName { get; set; } = "EventHandlingService";
    public int MaxHandlerAttempts { get; set; } = 3;
    public TimeSpan HandlerRetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan ConsumeErrorDelay { get; set; } = TimeSpan.FromSeconds(5);
    public Func<IntegrationEvent, bool> AcceptEvent { get; set; } = _ => true;
}
