using Confluent.Kafka;
using EventBus;
using EventBus.Abstractions;
using EventBus.Events;
using ProductCatalog.SearchSyncService.EventHandlers;

namespace ProductCatalog.SearchSyncService;

public class EventHandlingService(IConsumer<string, MessageEnvelop> consumer,
    EventHandlingWorkerOptions options,
    IIntegrationEventFactory integrationEventFactory,
    IServiceScopeFactory serviceScopeFactory,
    ILoggerFactory loggerFactory) : BackgroundService
{
    private readonly IConsumer<string, MessageEnvelop> consumer = consumer;
    private readonly EventHandlingWorkerOptions options = options;
    private readonly IIntegrationEventFactory integrationEventFactory = integrationEventFactory;
    private readonly IServiceScopeFactory serviceScopeFactory = serviceScopeFactory;
    private readonly ILogger logger = loggerFactory.CreateLogger(options.ServiceName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Subcribing to topics [{topics}]...", string.Join(',', options.Topics));

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
                    logger.LogWarning("No handler found for event type: {t}", message.MessageTypeName);
                    return;
                }

                try 
                {
                    await handler.HandleAsync(evt, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error handling event of type: {t}", message.MessageTypeName);
                }
            }
            else
            {
                logger.LogDebug("Event skipped: {t}", message.MessageTypeName);
            }
        }
        else
        {
            logger.LogWarning("Event type not found: {t}", message.MessageTypeName);
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
