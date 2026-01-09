using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting.Testing;
using FluentAssertions;
using ProductCatalog.Events;
using ProductCatalog.Infrastructure.Entity;

namespace ProductCatalog.IntegrationTests;

public class CatalogApiIntegrationTests : IAsyncLifetime
{
    private DistributedApplicationTestingBuilder? _builder;
    private DistributedApplication? _app;
    private HttpClient? _httpClient;

    public async Task InitializeAsync()
    {
        // Create an Aspire testing builder
        _builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ProductCatalog_AppHost>();

        // Build the application
        _app = await _builder.BuildAsync();

        // Start the application
        await _app.StartAsync();

        // Get the HTTP client for the API
        _httpClient = _app.CreateHttpClient("catalog-api");
    }

    public async Task DisposeAsync()
    {
        if (_app != null)
        {
            await _app.DisposeAsync();
        }

        _httpClient?.Dispose();
    }

    [Fact]
    public async Task CreateBrand_ShouldReturnCreatedBrand()
    {
        // Arrange
        var brand = new Brand
        {
            Name = $"Integration Test Brand {Guid.NewGuid()}",
            Description = "Test Description",
            UrlSlug = $"test-brand-{Guid.NewGuid()}",
            LogoUrl = "https://example.com/logo.png"
        };

        // Act
        var response = await _httpClient!.PostAsJsonAsync("/api/v1/brands", brand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdBrand = await response.Content.ReadFromJsonAsync<Brand>();
        createdBrand.Should().NotBeNull();
        createdBrand!.Id.Should().NotBe(Guid.Empty);
        createdBrand.Name.Should().Be(brand.Name);
        createdBrand.Description.Should().Be(brand.Description);
        createdBrand.UrlSlug.Should().Be(brand.UrlSlug);
    }

    [Fact]
    public async Task GetBrands_ShouldReturnListOfBrands()
    {
        // Arrange
        var brand1 = new Brand
        {
            Name = $"Brand 1 {Guid.NewGuid()}",
            Description = "Brand 1 Description",
            UrlSlug = $"brand-1-{Guid.NewGuid()}"
        };

        var brand2 = new Brand
        {
            Name = $"Brand 2 {Guid.NewGuid()}",
            Description = "Brand 2 Description",
            UrlSlug = $"brand-2-{Guid.NewGuid()}"
        };

        await _httpClient!.PostAsJsonAsync("/api/v1/brands", brand1);
        await _httpClient.PostAsJsonAsync("/api/v1/brands", brand2);

        // Act
        var response = await _httpClient.GetAsync("/api/v1/brands");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var brands = await response.Content.ReadFromJsonAsync<Brand[]>();
        brands.Should().NotBeNull();
        brands!.Length.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task UpdateBrand_ShouldUpdateExistingBrand()
    {
        // Arrange
        var brand = new Brand
        {
            Name = $"Original Brand {Guid.NewGuid()}",
            Description = "Original Description",
            UrlSlug = $"original-brand-{Guid.NewGuid()}",
            LogoUrl = "https://example.com/original.png"
        };

        var createResponse = await _httpClient!.PostAsJsonAsync("/api/v1/brands", brand);
        var createdBrand = await createResponse.Content.ReadFromJsonAsync<Brand>();

        createdBrand!.Name = "Updated Brand Name";
        createdBrand.Description = "Updated Description";
        createdBrand.UrlSlug = $"updated-brand-{Guid.NewGuid()}";

        // Act
        var updateResponse = await _httpClient.PutAsJsonAsync($"/api/v1/brands/{createdBrand.Id}", createdBrand);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedBrand = await updateResponse.Content.ReadFromJsonAsync<Brand>();
        updatedBrand.Should().NotBeNull();
        updatedBrand!.Name.Should().Be("Updated Brand Name");
        updatedBrand.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task CreateCategory_ShouldReturnCreatedCategory()
    {
        // Arrange
        var category = new Category
        {
            Name = $"Integration Test Category {Guid.NewGuid()}",
            Description = "Test Category Description",
            UrlSlug = $"test-category-{Guid.NewGuid()}"
        };

        // Act
        var response = await _httpClient!.PostAsJsonAsync("/api/v1/categories", category);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdCategory = await response.Content.ReadFromJsonAsync<Category>();
        createdCategory.Should().NotBeNull();
        createdCategory!.Id.Should().NotBe(Guid.Empty);
        createdCategory.Name.Should().Be(category.Name);
    }

    [Fact]
    public async Task CreateDimension_ShouldReturnCreatedDimension()
    {
        // Arrange
        var dimensions = new[]
        {
            new Dimension
            {
                Id = $"test_dimension_{Guid.NewGuid().ToString().Replace("-", "_")}",
                Name = "Test Dimension",
                DisplayType = "dropdown",
                Values = new List<DimensionValue>
                {
                    new DimensionValue { Value = "Value1", DisplayValue = "Value 1" },
                    new DimensionValue { Value = "Value2", DisplayValue = "Value 2" }
                }
            }
        };

        // Act
        var response = await _httpClient!.PostAsJsonAsync("/api/v1/dimensions", dimensions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdDimensions = await response.Content.ReadFromJsonAsync<Dimension[]>();
        createdDimensions.Should().NotBeNull();
        createdDimensions!.Should().ContainSingle();
        createdDimensions[0].Name.Should().Be("Test Dimension");
        createdDimensions[0].Values.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetDimensions_ShouldReturnListOfDimensions()
    {
        // Arrange
        var dimensions = new[]
        {
            new Dimension
            {
                Id = $"dim1_{Guid.NewGuid().ToString().Replace("-", "_")}",
                Name = "Dimension 1",
                DisplayType = "dropdown"
            }
        };

        await _httpClient!.PostAsJsonAsync("/api/v1/dimensions", dimensions);

        // Act
        var response = await _httpClient.GetAsync("/api/v1/dimensions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<Dimension>>();
        result.Should().NotBeNull();
        result!.Should().NotBeEmpty();
    }
}
