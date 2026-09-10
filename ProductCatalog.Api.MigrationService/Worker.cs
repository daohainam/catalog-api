using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Diagnostics;
using OpenTelemetry.Trace;
using ProductCatalog.Infrastructure.Data;

namespace ProductCatalog.Api.MigrationService;

public class Worker(IServiceProvider serviceProvider,
IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource activitySource = new(ActivitySourceName);
    
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var activity = activitySource.StartActivity("Migrating database", ActivityKind.Client);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProductCatalogDbContext>();

            await EnsureDatabaseAsync(dbContext, cancellationToken);
            await RunMigrationAsync(dbContext, cancellationToken);

            await CreateOutboxNotificationTriggerAsync(dbContext, cancellationToken);

            await SeedDataAsync(dbContext, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }

    /// <summary>
    /// Signals the outbox service that new rows are available.
    ///
    /// The notification deliberately carries only the row id: pg_notify caps a
    /// payload at 8000 bytes, and sending the whole row made the AFTER INSERT
    /// trigger raise on any large product, rolling back the caller's transaction.
    /// The outbox service reads the rows from the table itself.
    /// </summary>
    private static async Task CreateOutboxNotificationTriggerAsync(ProductCatalogDbContext dbContext, CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE OR REPLACE FUNCTION notify_outbox_change() RETURNS trigger AS $$
            BEGIN
              PERFORM pg_notify('outbox_channel', NEW."Id"::text);
              RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            CREATE OR REPLACE TRIGGER outbox_change_trigger
            AFTER INSERT ON "LogTailingOutboxMessages"
            FOR EACH ROW EXECUTE FUNCTION notify_outbox_change();
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static async Task EnsureDatabaseAsync(ProductCatalogDbContext dbContext, CancellationToken cancellationToken)
    {
        var dbCreator = dbContext.GetService<IRelationalDatabaseCreator>();

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Create the database if it does not exist.
            // Do this first so there is then a database to start a transaction against.
            if (!await dbCreator.ExistsAsync(cancellationToken))
            {
                await dbCreator.CreateAsync(cancellationToken);
            }
        });
    }

    private static async Task RunMigrationAsync(ProductCatalogDbContext dbContext, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        });
    }

    private static Task SeedDataAsync(ProductCatalogDbContext dbContext, CancellationToken cancellationToken)
    {
        // Seed data here.
        return Task.CompletedTask;

        //var strategy = dbContext.Database.CreateExecutionStrategy();
        //await strategy.ExecuteAsync(async () =>
        //{
        //    await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        //    await dbContext.SaveChangesAsync(cancellationToken);
        //    await transaction.CommitAsync(cancellationToken);
        //});
    }
}
