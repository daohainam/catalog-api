using EventBus.Kafka;

namespace ProductCatalog.SearchSyncService.Extensions;

public static class KafkaEventBusExtensions
{
    public static IHostApplicationBuilder AddKafkaEventConsumer(this IHostApplicationBuilder builder, Action<EventHandlingWorkerOptions>? configureOptions = null)
    {
        var options = new EventHandlingWorkerOptions();
        configureOptions?.Invoke(options);

        if (options.IntegrationEventFactory is null)
        {
            throw new InvalidOperationException(
                $"{nameof(EventHandlingWorkerOptions)}.{nameof(EventHandlingWorkerOptions.IntegrationEventFactory)} must be configured.");
        }

        if (options.Topics.Count == 0)
        {
            throw new InvalidOperationException(
                $"{nameof(EventHandlingWorkerOptions)}.{nameof(EventHandlingWorkerOptions.Topics)} must contain at least one topic.");
        }

        builder.AddKafkaMessageEnvelopConsumer(options.KafkaGroupId);
        builder.AddKafkaProducer("kafka");
        builder.AddKafkaDeadLetterPublisher(options.DeadLetterTopic);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(_ => options.IntegrationEventFactory!);
        builder.Services.AddHostedService<EventHandlingService>();

        return builder;
    }
}
