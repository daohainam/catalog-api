using Confluent.Kafka;
using EventBus;
using EventBus.Abstractions;
using EventBus.Events;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Data;
using ProductCatalog.Infrastructure.Entity;
using ProductCatalog.SearchSyncService.EventHandlers;

namespace ProductCatalog.SearchSyncService;

public class EventHandlingService(IConsumer<string, MessageEnvelop> consumer,
    EventHandlingWorkerOptions options,
    IIntegrationEventFactory integrationEventFactory,
    IServiceScopeFactory serviceScopeFactory,
    ILoggerFactory loggerFactory) : BackgroundService
{
    private const int MaxRetryAttempts = 3;

    private readonly IConsumer<string, MessageEnvelop> consumer = consumer;
    private readonly EventHandlingWorkerOptions options = options;
    private readonly IIntegrationEventFactory integrationEventFactory = integrationEventFactory;
    private readonly IServiceScopeFactory serviceScopeFactory = serviceScopeFactory;
    private readonly ILogger logger = loggerFactory.CreateLogger(options.ServiceName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Subscribing to topics [{topics}]...", string.Join(',', options.Topics));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    consumer.Subscribe(options.Topics);

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            var consumeResult = consumer.Consume(100);

                            if (consumeResult != null)
                            {
                                using IServiceScope scope = serviceScopeFactory.CreateScope();
                                await ProcessMessageAsync(scope.ServiceProvider, consumeResult.Message.Value, stoppingToken);
                            }
                            else
                            {
                                logger.LogDebug("No message consumed, waiting...");
                                await Task.Delay(100, stoppingToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error consuming message");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error subscribing to topics");
                }

                await Task.Delay(1000, stoppingToken);
            }
        }
    }
    private async Task<bool> RetryWithBackoffAsync(IEventHandler handler, IntegrationEvent evt, CancellationToken cancellationToken, string messageTypeName)
    {
        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // 1s, 2s, 4s
            logger.LogInformation("Retry attempt {attempt}/{maxRetries} for event type: {t} in {delay}s",
                attempt, MaxRetryAttempts, messageTypeName, delay.TotalSeconds);

            await Task.Delay(delay, cancellationToken);

            try
            {
                await handler.HandleAsync(evt, cancellationToken);
                logger.LogInformation("Retry attempt {attempt} succeeded for event type: {t}", attempt, messageTypeName);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw; // Don't retry on cancellation
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Retry attempt {attempt}/{maxRetries} failed for event type: {t}", attempt, MaxRetryAttempts, messageTypeName);
            }
        }

        return false;
    }

    private async Task ProcessMessageAsync(IServiceProvider services, MessageEnvelop message, CancellationToken cancellationToken)
    {
        var evt = integrationEventFactory.CreateEvent(message.MessageTypeName, message.Message);

        if (evt is not null)
        {
            if (options.AcceptEvent(evt))
            {
                logger.LogInformation("Processing message {t}: {message}", message.MessageTypeName, message.Message);

                var handlerFactory = services.GetRequiredService<IEventHandlerFactory>();
                var handler = handlerFactory.CreateHandler(services, evt);

                if (handler is null)
                {
                    logger.LogWarning("No handler found for event type: {t}. Message will be skipped.", message.MessageTypeName);
                    return;
                }

                try 
                {
                    await handler.HandleAsync(evt, cancellationToken);
                    logger.LogDebug("Successfully handled event of type: {t}", message.MessageTypeName);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error handling event of type: {t}. Retrying with exponential backoff.", message.MessageTypeName);
                    var handled = await RetryWithBackoffAsync(handler, evt, cancellationToken, message.MessageTypeName);
                    if (!handled)
                    {
                        logger.LogError("Event of type: {t} failed after all retry attempts. Writing to dead-letter store.", message.MessageTypeName);
                        await WriteToDeadLetterAsync(services, message, ex);
                    }
                }
            }
            else
            {
                logger.LogDebug("Event skipped: {t}", message.MessageTypeName);
            }
        }
        else
        {
            logger.LogWarning("Event type not found: {t}. Message will be skipped.", message.MessageTypeName);
        }
    }

    private async Task WriteToDeadLetterAsync(IServiceProvider services, MessageEnvelop message, Exception lastException)
    {
        try
        {
            var dbContext = services.GetRequiredService<ProductCatalogDbContext>();
            var deadLetterEvent = new DeadLetterEvent
            {
                Id = Guid.CreateVersion7(),
                EventTypeName = message.MessageTypeName,
                Payload = message.Message,
                ErrorMessage = lastException.Message,
                StackTrace = lastException.StackTrace,
                RetryCount = MaxRetryAttempts,
                FailedAt = DateTime.UtcNow,
                IsReprocessed = false
            };

            await dbContext.DeadLetterEvents.AddAsync(deadLetterEvent);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Dead-letter event written for type: {t}, Id: {id}", message.MessageTypeName, deadLetterEvent.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write dead-letter event for type: {t}. Event data: {payload}", message.MessageTypeName, message.Message);
        }
    }
}

public class EventHandlingWorkerOptions
{
    public string KafkaGroupId { get; set; } = "event-handling";
    public List<string> Topics { get; set; } = [];
    public IIntegrationEventFactory IntegrationEventFactory { get; set; } = EventBus.IntegrationEventFactory.Instance;
    public string ServiceName { get; set; } = "EventHandlingService";
    public Func<IntegrationEvent, bool> AcceptEvent { get; set; } = _ => true;
}
