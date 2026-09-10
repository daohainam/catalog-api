using EventBus.Events;

namespace EventBus.Kafka;
public static class KafkaEventBusExtensions
{
    public static IHostApplicationBuilder AddKafkaProducer(this IHostApplicationBuilder builder, string connectionName)
    {
        builder.AddKafkaProducer<string, MessageEnvelop>(connectionName,
            configureSettings: (settings) =>
            {
                // Without idempotence a librdkafka retry can write the same record twice.
                settings.Config.EnableIdempotence = true;
                settings.Config.Acks = Acks.All;
            },
            configureBuilder: (builder) =>
            {
                builder.SetValueSerializer(new MessageEnvelopSerializer());
            }
            );

        return builder;
    }

    public static void AddKafkaEventPublisher(this IHostApplicationBuilder builder, string? topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        builder.Services.AddTransient<IEventPublisher>(services => new KafkaEventPublisher(
            topic,
            services.GetRequiredService<IProducer<string, MessageEnvelop>>(),
            services.GetRequiredService<ILoggerFactory>().CreateLogger($"EventPublisher<{topic}>")
            ));
    }

    public static void AddKafkaDeadLetterPublisher(this IHostApplicationBuilder builder, string? topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        builder.Services.AddSingleton<IDeadLetterPublisher>(services => new KafkaDeadLetterPublisher(
            topic,
            services.GetRequiredService<IProducer<string, MessageEnvelop>>(),
            services.GetRequiredService<ILoggerFactory>().CreateLogger($"DeadLetterPublisher<{topic}>")
            ));
    }

    public static IHostApplicationBuilder AddKafkaMessageEnvelopConsumer(this IHostApplicationBuilder builder, string groupId, string connectionName = "kafka")
    {
        builder.AddKafkaConsumer<string, MessageEnvelop>(connectionName, configureSettings: (settings) => {
            settings.Config.GroupId = groupId;
            settings.Config.AutoOffsetReset = AutoOffsetReset.Earliest;
            // Offsets are committed by the consumer loop only after a message has
            // been handled (or dead-lettered). With auto-commit a handler failure
            // still advanced the offset, losing the message permanently.
            settings.Config.EnableAutoCommit = false;
        },
        configureBuilder: (builder) =>
        {
            builder.SetValueDeserializer(new MessageEnvelopDeserializer());
        }
        );

        return builder;
    }

    public static bool IsEvent<T1>(this IntegrationEvent @event)
    {
        return @event.GetType() == typeof(T1);
    }
    
    public static bool IsEvent<T1, T2>(this IntegrationEvent @event)
    {
        return @event.GetType() == typeof(T1) || @event.GetType() == typeof(T2);
    }

    public static bool IsEvent<T1, T2, T3>(this IntegrationEvent @event)
    {
        return @event.GetType() == typeof(T1) || @event.GetType() == typeof(T2) || @event.GetType() == typeof(T3);
    }

    public static bool IsEvent<T1, T2, T3, T4>(this IntegrationEvent @event)
    {
        return @event.GetType() == typeof(T1) || @event.GetType() == typeof(T2) || @event.GetType() == typeof(T3) || @event.GetType() == typeof(T4);
    }
}

internal class MessageEnvelopDeserializer : IDeserializer<MessageEnvelop>
{
    public MessageEnvelop Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
    {
        // A tombstone (null value) is a legitimate Kafka record, not a failure.
        // Throwing here surfaced as a consume error and skipped the offset, so the
        // null is handed to the consumer loop to skip and commit explicitly.
        if (isNull || data.IsEmpty)
        {
            return null!;
        }

        return JsonSerializer.Deserialize<MessageEnvelop>(data)!;
    }
}

internal class MessageEnvelopSerializer : ISerializer<MessageEnvelop>
{
    public byte[] Serialize(MessageEnvelop data, SerializationContext context)
    {
        return JsonSerializer.SerializeToUtf8Bytes(data);
    }
}
