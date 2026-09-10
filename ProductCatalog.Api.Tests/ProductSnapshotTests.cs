using FluentAssertions;
using ProductCatalog.Api.Models;
using ProductCatalog.Infrastructure.Entity;
using System.Text.Json;

namespace ProductCatalog.Api.Tests;

/// <summary>
/// Reverting used to read history JSON with JsonElement.GetProperty, which throws
/// on a missing key or a null value - every one of these cases was a 500.
/// </summary>
public class ProductSnapshotTests
{
    private static Product SampleProduct() => new()
    {
        Id = Guid.CreateVersion7(),
        Name = "Sample",
        UrlSlug = "sample",
        Description = "A sample product",
        BrandId = Guid.CreateVersion7(),
        CategoryId = Guid.CreateVersion7(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsActive = true,
        IsDeleted = false,
        Version = 4
    };

    [Fact]
    public void TryParse_RoundTripsASnapshot()
    {
        var product = SampleProduct();

        var parsed = ProductSnapshot.TryParse(JsonSerializer.Serialize(ProductSnapshot.From(product)));

        parsed.Should().NotBeNull();
        parsed!.Name.Should().Be(product.Name);
        parsed.UrlSlug.Should().Be(product.UrlSlug);
        parsed.Description.Should().Be(product.Description);
        parsed.BrandId.Should().Be(product.BrandId);
        parsed.CategoryId.Should().Be(product.CategoryId);
        parsed.IsActive.Should().Be(product.IsActive);
        parsed.IsDeleted.Should().Be(product.IsDeleted);
        parsed.Version.Should().Be(product.Version);
        parsed.SchemaVersion.Should().Be(ProductSnapshot.CurrentSchemaVersion);
    }

    [Fact]
    public void TryParse_ReadsRowsWrittenBeforeSchemaVersionExisted()
    {
        // The original snapshot was an anonymous object with these exact names.
        var legacy = JsonSerializer.Serialize(new
        {
            Id = Guid.CreateVersion7(),
            Name = "Legacy",
            UrlSlug = "legacy",
            Description = "Written by the previous version",
            BrandId = Guid.CreateVersion7(),
            CategoryId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true,
            IsDeleted = false,
            Version = 2L
        });

        var parsed = ProductSnapshot.TryParse(legacy);

        parsed.Should().NotBeNull();
        parsed!.Name.Should().Be("Legacy");
        parsed.SchemaVersion.Should().Be(0);
    }

    [Fact]
    public void TryParse_RejectsANewerSchema()
    {
        var future = """{"SchemaVersion":999,"Name":"n","UrlSlug":"s","Description":"d"}""";

        ProductSnapshot.TryParse(future).Should().BeNull();
    }

    [Theory]
    [InlineData("""{"Name":null,"UrlSlug":"s","Description":"d"}""")]
    [InlineData("""{"UrlSlug":"s","Description":"d"}""")]
    [InlineData("""{"Name":"n","Description":"d"}""")]
    [InlineData("""{"Name":"n","UrlSlug":"s"}""")]
    public void TryParse_RejectsSnapshotsMissingRequiredValues(string json)
    {
        ProductSnapshot.TryParse(json).Should().BeNull();
    }

    [Fact]
    public void TryParse_RejectsMalformedJson()
    {
        ProductSnapshot.TryParse("not json").Should().BeNull();
    }
}
