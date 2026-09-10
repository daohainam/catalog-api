using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ProductCatalog.Api.EndpointTests;

/// <summary>
/// Route registration is resolved lazily, so a bad group prefix surfaces as a 500
/// on every request rather than a startup failure. Binding the catalog group to an
/// api-version set while the prefix also carried "v{version:apiVersion}" did
/// exactly that - the version parameter appeared twice and the whole group broke.
///
/// No database is needed: only the endpoint table is inspected.
/// </summary>
public class RouteRegistrationTests
{
    private sealed class RoutesOnlyFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:catalogdb", "Host=localhost;Database=unused;Username=unused;Password=unused");
            builder.UseEnvironment("Development");
        }
    }

    [Fact]
    public void CatalogEndpointsResolveUnderApiV1()
    {
        using var factory = new RoutesOnlyFactory();

        var patterns = factory.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText)
            .ToList();

        patterns.Should().Contain("/api/v1/products/");
        patterns.Should().Contain("/api/v1/products/{productId:guid}/history");
        patterns.Should().Contain("/api/v1/products/{productId:guid}/history/{version:long}/revert");
        patterns.Should().Contain("/api/v1/brands/{brandId:guid}");
        patterns.Should().NotContain(p => p!.Contains("apiVersion"));
    }
}
