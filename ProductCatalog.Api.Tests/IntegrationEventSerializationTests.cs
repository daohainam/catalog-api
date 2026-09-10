using FluentAssertions;
using ProductCatalog.Events;
using System.Text.Json;

namespace ProductCatalog.Api.Tests;

public class IntegrationEventSerializationTests
{
    [Fact]
    public void EventIdAndCreationDateSurviveARoundTrip()
    {
        var original = new ProductCreatedEvent
        {
            ProductId = Guid.CreateVersion7(),
            Product = new ProductInfo { Name = "Sample", UrlSlug = "sample", Description = "" }
        };

        var restored = JsonSerializer.Deserialize<ProductCreatedEvent>(JsonSerializer.Serialize(original));

        // With private setters these were silently regenerated on deserialization,
        // so the id logged by the publisher never matched the consumer's.
        restored!.EventId.Should().Be(original.EventId);
        restored.EventCreationDate.Should().Be(original.EventCreationDate);
    }

    [Fact]
    public void ProductEventsPartitionByProductId()
    {
        var productId = Guid.CreateVersion7();

        new ProductCreatedEvent { ProductId = productId }.PartitionKey.Should().Be(productId.ToString());
        new ProductUpdatedEvent { ProductId = productId }.PartitionKey.Should().Be(productId.ToString());
        new ProductDeletedEvent { ProductId = productId }.PartitionKey.Should().Be(productId.ToString());
    }

    [Fact]
    public void PartitionKeyIsNotSerialized()
    {
        var json = JsonSerializer.Serialize(new ProductCreatedEvent { ProductId = Guid.CreateVersion7() });

        json.Should().NotContain("PartitionKey");
    }
}
