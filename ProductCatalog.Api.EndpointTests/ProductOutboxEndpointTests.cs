using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Entity;
using System.Net;
using System.Net.Http.Json;

namespace ProductCatalog.Api.EndpointTests;

/// <summary>
/// Every write that changes a product must queue an event, otherwise the search
/// index silently diverges. For a long time only CreateProduct did.
/// </summary>
[Collection(CatalogApiCollection.Name)]
public class ProductOutboxEndpointTests(CatalogApiFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task CreateProduct_QueuesAProductCreatedEvent()
    {
        var product = await CreateProductAsync();

        var payloadTypes = await OutboxPayloadTypesAsync(product.Id);

        payloadTypes.Should().ContainSingle()
            .Which.Should().Be(typeof(ProductCatalog.Events.ProductCreatedEvent).FullName);
    }

    [Fact]
    public async Task UpdateProduct_QueuesAProductUpdatedEvent()
    {
        var product = await CreateProductAsync();

        var response = await client.PutAsJsonAsync($"/api/v1/products/{product.Id}", new Product
        {
            Id = product.Id,
            Name = "Renamed",
            UrlSlug = product.UrlSlug,
            Description = product.Description,
            BrandId = product.BrandId,
            CategoryId = product.CategoryId,
            IsActive = true
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payloadTypes = await OutboxPayloadTypesAsync(product.Id);

        payloadTypes.Should().Contain(typeof(ProductCatalog.Events.ProductUpdatedEvent).FullName);
    }

    [Fact]
    public async Task RevertProduct_QueuesAProductUpdatedEvent()
    {
        var product = await CreateProductAsync();

        await client.PutAsJsonAsync($"/api/v1/products/{product.Id}", new Product
        {
            Id = product.Id,
            Name = "Renamed",
            UrlSlug = product.UrlSlug,
            Description = product.Description,
            BrandId = product.BrandId,
            CategoryId = product.CategoryId,
            IsActive = true
        });

        var response = await client.PostAsync($"/api/v1/products/{product.Id}/history/1/revert", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payloadTypes = await OutboxPayloadTypesAsync(product.Id);

        payloadTypes.Count(t => t == typeof(ProductCatalog.Events.ProductUpdatedEvent).FullName)
            .Should().Be(2);
    }

    [Fact]
    public async Task QueuedEventsStartOutUnprocessed()
    {
        var product = await CreateProductAsync();

        using var dbContext = factory.CreateDbContext();
        var messages = await dbContext.LogTailingOutboxMessages
            .Where(m => m.Payload.Contains(product.Id.ToString()))
            .ToListAsync();

        messages.Should().NotBeEmpty();
        messages.Should().OnlyContain(m => m.ProcessedAt == null);
    }

    private async Task<List<string>> OutboxPayloadTypesAsync(Guid productId)
    {
        using var dbContext = factory.CreateDbContext();
        return await dbContext.LogTailingOutboxMessages
            .Where(m => m.Payload.Contains(productId.ToString()))
            .OrderBy(m => m.CreationDate)
            .Select(m => m.PayloadType)
            .ToListAsync();
    }

    private async Task<Product> CreateProductAsync()
    {
        var brandResponse = await client.PostAsJsonAsync("/api/v1/brands", new Brand
        {
            Name = $"Brand {Guid.NewGuid()}",
            UrlSlug = $"brand-{Guid.NewGuid()}"
        });
        var brand = (await brandResponse.Content.ReadFromJsonAsync<Brand>())!;

        var categoryResponse = await client.PostAsJsonAsync("/api/v1/categories", new Category
        {
            Name = $"Category {Guid.NewGuid()}",
            UrlSlug = $"category-{Guid.NewGuid()}"
        });
        var category = (await categoryResponse.Content.ReadFromJsonAsync<Category>())!;

        var response = await client.PostAsJsonAsync("/api/v1/products", new Product
        {
            Name = $"Product {Guid.NewGuid()}",
            UrlSlug = $"slug-{Guid.NewGuid()}",
            Description = "A product",
            BrandId = brand.Id,
            CategoryId = category.Id,
            IsActive = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<Product>())!;
    }
}
