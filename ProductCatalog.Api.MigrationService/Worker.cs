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

            // Create trigger for notifying outbox changes
            await dbContext.Database.ExecuteSqlAsync($"CREATE OR REPLACE FUNCTION notify_outbox_change() RETURNS trigger AS $$\r\nBEGIN\r\n  PERFORM pg_notify('outbox_channel', row_to_json(NEW)::text);\r\n  RETURN NEW;\r\nEND;\r\n$$ LANGUAGE plpgsql;\r\n\r\nCREATE OR REPLACE TRIGGER outbox_change_trigger\r\nAFTER INSERT ON \"LogTailingOutboxMessages\"\r\nFOR EACH ROW EXECUTE FUNCTION notify_outbox_change();", cancellationToken: cancellationToken);

            await SeedDataAsync(dbContext, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }

        hostApplicationLifetime.StopApplication();
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
