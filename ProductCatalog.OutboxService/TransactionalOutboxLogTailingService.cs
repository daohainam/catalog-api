using EventBus.Abstractions;
using EventBus.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using ProductCatalog.Infrastructure.Data;
using ProductCatalog.Infrastructure.Entity;
using System.Text.Json;
using System.Threading.Channels;

namespace ProductCatalog.OutboxService;

/// <summary>
/// Publishes rows from the outbox table to the event bus.
///
/// A Postgres LISTEN/NOTIFY signal wakes the service so new rows are picked up
/// promptly, but the notification is only a hint: the rows themselves are read
/// from the table and stamped with ProcessedAt once published. A periodic sweep
/// runs regardless, so a missed notification, a publisher restart or a broker
/// outage delays events rather than losing them.
/// </summary>
internal class TransactionalOutboxLogTailingService : BackgroundService
{
    private readonly TransactionalOutboxLogTailingServiceOptions options;
    private readonly IEventPublisher eventPublisher;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<TransactionalOutboxLogTailingService> logger;

    // Capacity 1 with DropWrite: a pending wake-up already covers any further
    // notifications that arrive before the sweep runs.
    private readonly Channel<byte> wakeUp = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public TransactionalOutboxLogTailingService(TransactionalOutboxLogTailingServiceOptions options, IEventPublisher eventPublisher, IServiceScopeFactory serviceScopeFactory, ILoggerFactory loggerFactory)
    {
        this.options = options;
        this.eventPublisher = eventPublisher;
        this.serviceScopeFactory = serviceScopeFactory;
        logger = loggerFactory.CreateLogger<TransactionalOutboxLogTailingService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting TransactionOutbox log tailing service...");

        var listener = ListenForNotificationsAsync(stoppingToken);
        var publisher = PublishPendingLoopAsync(stoppingToken);

        await Task.WhenAll(listener, publisher);
    }

    private async Task ListenForNotificationsAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var conn = new NpgsqlConnection(options.ConnectionString);
                await conn.OpenAsync(stoppingToken);

                // Synchronous handler: it only signals. Doing async work in a
                // Notification handler makes it async void, which puts any
                // exception outside every catch in this class.
                conn.Notification += (_, _) => wakeUp.Writer.TryWrite(0);

                await using (var cmd = new NpgsqlCommand($"LISTEN {options.NotificationChannel}", conn))
                {
                    await cmd.ExecuteNonQueryAsync(stoppingToken);
                }

                // Anything inserted before LISTEN took effect is caught by this sweep.
                wakeUp.Writer.TryWrite(0);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await conn.WaitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("TransactionalOutbox log tailing service is stopping");
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error listening for outbox notifications. Reconnecting in {Delay}s...",
                    options.RetryDelay.TotalSeconds);
                await DelayAsync(options.RetryDelay, stoppingToken);
            }
        }
    }

    private async Task PublishPendingLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.SweepInterval);
        var nextCleanup = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(stoppingToken);

                if (DateTime.UtcNow >= nextCleanup)
                {
                    await CleanUpProcessedAsync(stoppingToken);
                    nextCleanup = DateTime.UtcNow.Add(options.CleanUpInterval);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing outbox messages. Retrying in {Delay}s...",
                    options.RetryDelay.TotalSeconds);
                await DelayAsync(options.RetryDelay, stoppingToken);
                continue;
            }

            // Whichever comes first: a notification, or the periodic sweep.
            var wake = wakeUp.Reader.WaitToReadAsync(stoppingToken).AsTask();
            var tick = timer.WaitForNextTickAsync(stoppingToken).AsTask();

            try
            {
                var completed = await Task.WhenAny(wake, tick);
                if (completed == wake && wake.Result)
                {
                    wakeUp.Reader.TryRead(out _);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        // A scope per sweep: a DbContext is not thread-safe and must not be held
        // for the lifetime of a singleton background service.
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProductCatalogDbContext>();

        while (!cancellationToken.IsCancellationRequested)
        {
            var pending = await dbContext.LogTailingOutboxMessages
                .Where(m => m.ProcessedAt == null)
                .OrderBy(m => m.CreationDate)
                .Take(options.BatchSize)
                .ToListAsync(cancellationToken);

            if (pending.Count == 0)
            {
                return;
            }

            foreach (var message in pending)
            {
                var @event = RebuildEvent(message);

                if (@event == null)
                {
                    // Nothing can be done with this row; stamp it so it stops
                    // blocking the queue, and leave the warning for triage.
                    logger.LogWarning("Failed to rebuild event from outbox message {MessageId} of type {PayloadType}",
                        message.Id, message.PayloadType);
                    message.ProcessedAt = DateTime.UtcNow;
                    continue;
                }

                if (!await eventPublisher.PublishAsync(@event, cancellationToken))
                {
                    // Leave ProcessedAt null so the next sweep retries. Stop here
                    // so events keep their creation order.
                    logger.LogWarning("Publishing outbox message {MessageId} failed; it will be retried", message.Id);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return;
                }

                message.ProcessedAt = DateTime.UtcNow;
                logger.LogDebug("Published outbox message {MessageId} of type {PayloadType}", message.Id, message.PayloadType);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (pending.Count < options.BatchSize)
            {
                return;
            }
        }
    }

    private async Task CleanUpProcessedAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProductCatalogDbContext>();

        var cutoff = DateTime.UtcNow.Subtract(options.Retention);
        var deleted = await dbContext.LogTailingOutboxMessages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            logger.LogInformation("Removed {Count} outbox messages processed before {Cutoff}", deleted, cutoff);
        }
    }

    private IntegrationEvent? RebuildEvent(LogTailingOutboxMessage message)
    {
        var type = options.PayloadTypeResolver(message.PayloadType);

        if (type == null)
        {
            logger.LogWarning("Failed to find type {PayloadType}", message.PayloadType);
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(message.Payload, type) as IntegrationEvent;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize outbox message {MessageId}", message.Id);
            return null;
        }
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}

public class TransactionalOutboxLogTailingServiceOptions
{
    public Func<string, Type?> PayloadTypeResolver { get; set; } = Type.GetType;
    public required string ConnectionString { get; set; }
    public string NotificationChannel { get; set; } = "outbox_channel";
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    public int BatchSize { get; set; } = 100;
    public TimeSpan CleanUpInterval { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);
}
