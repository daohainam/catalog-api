using Aspire.Hosting;
using k8s.KubeConfigModels;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using ProductCatalog.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FunctionalTests
{
    public class ProductCatalogApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly IResourceBuilder<PostgresServerResource> postgres;
        private readonly DistributedApplication app;

        public string DbConnectionString { get; private set; } = default!;
        public CatalogContext DbContext { get; private set; } = default!;

        public ProductCatalogApiFixture()
        {
            var options = new DistributedApplicationOptions { AssemblyName = typeof(ProductCatalogApiFixture).Assembly.FullName, DisableDashboard = true };
            var builder = DistributedApplication.CreateBuilder(options);
            postgres = builder.AddPostgres("postgres")
                .WithImageTag("latest");
            var catalogDb = postgres.AddDatabase("catalogdb", "catalog");

            app = builder.Build();            
        }

        public async Task InitializeAsync()
        {
            await app.StartAsync();
            
            var s = await app.GetConnectionStringAsync("catalogdb");

            var csBuilder = new Npgsql.NpgsqlConnectionStringBuilder(await postgres.Resource.GetConnectionStringAsync() ?? throw new InvalidOperationException("Could not get connection string"))
            {
                Database = "catalog"
            };
            DbConnectionString = csBuilder.ConnectionString;

            var optionsBuilder = new DbContextOptionsBuilder<CatalogContext>();
            optionsBuilder.UseNpgsql(DbConnectionString);
            DbContext = new CatalogContext(optionsBuilder.Options);

            CancellationToken cancellationToken = default;
            var dbCreator = DbContext.GetService<IRelationalDatabaseCreator>();
            if (!await dbCreator.ExistsAsync(cancellationToken))
            {
                await dbCreator.CreateAsync(cancellationToken);
            }

            await DbContext.Database.MigrateAsync(cancellationToken);
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            await base.DisposeAsync();
            await app.StopAsync();
            if (app is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                app.Dispose();
            }
        }
    }
}
