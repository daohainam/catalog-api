using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace ProductCatalog.Api.EndpointTests;

/// <summary>
/// Hosts the real API against a throwaway PostgreSQL container.
///
/// The unit tests in ProductCatalog.Api.Tests use the EF in-memory provider,
/// which ignores unique indexes, NOT NULL and concurrency tokens - exactly the
/// constraints the versioning and revert code depends on. These tests go through
/// HTTP against a real database instead, so the endpoints themselves are what is
/// under test rather than a copy of their logic.
/// </summary>
public sealed class CatalogApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProductCatalogDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await container.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:catalogdb", container.GetConnectionString());
        builder.UseEnvironment("Development");
    }

    /// <summary>
    /// Scope for inspecting or seeding the database directly. The caller owns the
    /// scope, so dispose it rather than just the DbContext.
    /// </summary>
    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();
}

[CollectionDefinition(Name)]
public sealed class CatalogApiCollection : ICollectionFixture<CatalogApiFactory>
{
    public const string Name = "catalog-api";
}
