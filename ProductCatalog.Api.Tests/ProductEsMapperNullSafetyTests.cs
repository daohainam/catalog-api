using FluentAssertions;
using ProductCatalog.Events;
using ProductCatalog.Search;
using System.Text.Json;

namespace ProductCatalog.Api.Tests;

/// <summary>
/// ProductInfo list-initializes its collections, but an explicit null in the
/// incoming JSON overwrites those initializers - and a NullReferenceException in
/// the mapper meant the message was retried three times and then dropped.
/// </summary>
public class ProductEsMapperNullSafetyTests
{
    [Fact]
    public void Map_HandlesAnEventWhoseCollectionsAreExplicitlyNull()
    {
        var json = """
        {
          "ProductId": "0195f3a0-0000-7000-8000-000000000001",
          "Product": {
            "Name": "Null collections",
            "UrlSlug": "null-collections",
            "Description": null,
            "IsActive": true,
            "Path": null,
            "Brand": null,
            "Variants": null,
            "Dimensions": null,
            "Groups": null,
            "Images": null
          }
        }
        """;

        var evt = JsonSerializer.Deserialize<ProductCreatedEvent>(json)!;

        var doc = ProductEsMapper.Map(evt);

        doc.Name.Should().Be("Null collections");
        doc.Description.Should().BeEmpty();
        doc.BrandName.Should().BeEmpty();
        doc.Variants.Should().BeEmpty();
        doc.Dimensions.Should().BeEmpty();
        doc.GroupIds.Should().BeEmpty();
        doc.Images.Should().BeEmpty();
        doc.PriceMin.Should().BeNull();
        doc.InStock.Should().BeFalse();
    }

    [Fact]
    public void Map_ProducesTheSameDocumentForCreatedAndUpdatedEvents()
    {
        var productId = Guid.CreateVersion7();
        var product = new ProductInfo
        {
            Name = "Shared",
            UrlSlug = "shared",
            Description = "Same payload either way",
            IsActive = true,
            Path = [],
            Brand = new BrandInfo { BrandId = Guid.CreateVersion7(), Name = "Brand" }
        };

        var created = ProductEsMapper.Map(new ProductCreatedEvent { ProductId = productId, Product = product });
        var updated = ProductEsMapper.Map(new ProductUpdatedEvent { ProductId = productId, Product = product });

        updated.Should().BeEquivalentTo(created);
    }
}
