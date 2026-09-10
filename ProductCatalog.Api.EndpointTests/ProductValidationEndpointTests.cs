using FluentAssertions;
using ProductCatalog.Infrastructure.Entity;
using System.Net;
using System.Net.Http.Json;

namespace ProductCatalog.Api.EndpointTests;

/// <summary>
/// The product endpoints used to validate nothing, so a malformed body reached
/// the event mapper and came back as a 500 instead of a 400.
/// </summary>
[Collection(CatalogApiCollection.Name)]
public class ProductValidationEndpointTests(CatalogApiFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task CreateProduct_WithAnEmptyBody_ReturnsBadRequest()
    {
        var response = await client.PostAsJsonAsync("/api/v1/products", new Product());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateProduct_WithAnUnknownBrand_ReturnsBadRequest()
    {
        var response = await client.PostAsJsonAsync("/api/v1/products", new Product
        {
            Name = "Orphan",
            UrlSlug = $"orphan-{Guid.NewGuid()}",
            Description = "No such brand",
            BrandId = Guid.CreateVersion7(),
            CategoryId = Guid.CreateVersion7()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProduct_WithAMismatchedId_ReturnsBadRequest()
    {
        var routeId = Guid.CreateVersion7();

        var response = await client.PutAsJsonAsync($"/api/v1/products/{routeId}", new Product
        {
            Id = Guid.CreateVersion7(),
            Name = "Mismatched",
            UrlSlug = "mismatched",
            Description = "Body id does not match the route",
            BrandId = Guid.CreateVersion7(),
            CategoryId = Guid.CreateVersion7()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateBrand_WithABlankName_ReturnsABrandSpecificMessage()
    {
        var created = await client.PostAsJsonAsync("/api/v1/brands", new Brand
        {
            Name = $"Brand {Guid.NewGuid()}",
            UrlSlug = $"brand-{Guid.NewGuid()}"
        });
        var brand = (await created.Content.ReadFromJsonAsync<Brand>())!;

        var response = await client.PutAsJsonAsync($"/api/v1/brands/{brand.Id}", new Brand
        {
            Id = brand.Id,
            Name = "   ",
            UrlSlug = brand.UrlSlug
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var message = await response.Content.ReadAsStringAsync();
        // Used to say "Category Name is required." in the brand endpoint.
        message.Should().Contain("Brand Name");
    }

    [Fact]
    public async Task Search_IsReachableUnderTheVersionedRoute()
    {
        var response = await client.GetAsync("/api/v1/products?limit=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
