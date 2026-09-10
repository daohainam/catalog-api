using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Api.Models;
using ProductCatalog.Infrastructure.Entity;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProductCatalog.Api.EndpointTests;

[Collection(CatalogApiCollection.Name)]
public class ProductHistoryEndpointTests(CatalogApiFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task UpdateProduct_WritesHistoryAtThePreviousVersionAndIncrementsTheProduct()
    {
        var product = await CreateProductAsync("Original name");

        var updated = await UpdateProductAsync(product, name: "Updated name");
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        var current = await GetProductAsync(product.Id);
        current!.Name.Should().Be("Updated name");
        current.Version.Should().Be(2);

        var history = await client.GetFromJsonAsync<List<ProductHistory>>($"/api/v1/products/{product.Id}/history");
        history.Should().ContainSingle();
        history![0].Version.Should().Be(1);

        var snapshot = JsonSerializer.Deserialize<ProductSnapshot>(history[0].ProductData);
        snapshot!.Name.Should().Be("Original name");
        snapshot.SchemaVersion.Should().Be(ProductSnapshot.CurrentSchemaVersion);
    }

    [Fact]
    public async Task GetProductHistory_ReturnsNotFoundForAnUnknownProduct()
    {
        var response = await client.GetAsync($"/api/v1/products/{Guid.CreateVersion7()}/history");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProductHistoryByVersion_ReturnsTheStoredSnapshot()
    {
        var product = await CreateProductAsync("Version one");
        await UpdateProductAsync(product, name: "Version two");

        var response = await client.GetAsync($"/api/v1/products/{product.Id}/history/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await response.Content.ReadFromJsonAsync<ProductHistory>();
        history!.Version.Should().Be(1);
    }

    [Fact]
    public async Task RevertProduct_RestoresTheHistoricalFieldsAsANewVersion()
    {
        var product = await CreateProductAsync("Original name");
        await UpdateProductAsync(product, name: "Changed name");

        var response = await client.PostAsync($"/api/v1/products/{product.Id}/history/1/revert", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reverted = await response.Content.ReadFromJsonAsync<Product>();
        reverted!.Name.Should().Be("Original name");
        // Reverting rolls forward rather than rewinding the version number.
        reverted.Version.Should().Be(3);
    }

    [Fact]
    public async Task RevertProduct_ReturnsNotFoundForAnUnknownVersion()
    {
        var product = await CreateProductAsync("Only version");

        var response = await client.PostAsync($"/api/v1/products/{product.Id}/history/99/revert", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevertProduct_ReturnsBadRequestWhenTheSnapshotCannotBeRead()
    {
        var product = await CreateProductAsync("Corrupt history");
        await UpdateProductAsync(product, name: "Second version");

        using var dbContext = factory.CreateDbContext();
        var history = await dbContext.ProductHistories.SingleAsync(h => h.ProductId == product.Id && h.Version == 1);
        history.ProductData = """{"SchemaVersion":999,"Name":"from the future"}""";
        await dbContext.SaveChangesAsync();

        var response = await client.PostAsync($"/api/v1/products/{product.Id}/history/1/revert", null);

        // Previously this threw out of JsonElement.GetProperty and surfaced as a 500.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProduct_ReturnsConflictWhenTheProductChangedUnderneath()
    {
        var product = await CreateProductAsync("Contended");

        // Simulate a concurrent writer by bumping the version behind the API's back.
        using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Products
                .Where(p => p.Id == product.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Version, p => p.Version + 1));
        }

        var stale = await UpdateProductAsync(product, name: "Loser");

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<Product> CreateProductAsync(string name)
    {
        var (brandId, categoryId) = await SeedBrandAndCategoryAsync();

        var response = await client.PostAsJsonAsync("/api/v1/products", new Product
        {
            Name = name,
            UrlSlug = $"slug-{Guid.NewGuid()}",
            Description = "A product",
            BrandId = brandId,
            CategoryId = categoryId,
            IsActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<Product>())!;
    }

    private Task<HttpResponseMessage> UpdateProductAsync(Product product, string name) =>
        client.PutAsJsonAsync($"/api/v1/products/{product.Id}", new Product
        {
            Id = product.Id,
            Name = name,
            UrlSlug = product.UrlSlug,
            Description = product.Description,
            BrandId = product.BrandId,
            CategoryId = product.CategoryId,
            IsActive = true
        });

    private Task<Product?> GetProductAsync(Guid productId) =>
        client.GetFromJsonAsync<Product>($"/api/v1/products/{productId}");

    private async Task<(Guid brandId, Guid categoryId)> SeedBrandAndCategoryAsync()
    {
        var brandResponse = await client.PostAsJsonAsync("/api/v1/brands", new Brand
        {
            Name = $"Brand {Guid.NewGuid()}",
            UrlSlug = $"brand-{Guid.NewGuid()}"
        });
        brandResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var brand = (await brandResponse.Content.ReadFromJsonAsync<Brand>())!;

        var categoryResponse = await client.PostAsJsonAsync("/api/v1/categories", new Category
        {
            Name = $"Category {Guid.NewGuid()}",
            UrlSlug = $"category-{Guid.NewGuid()}"
        });
        categoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var category = (await categoryResponse.Content.ReadFromJsonAsync<Category>())!;

        return (brand.Id, category.Id);
    }
}
