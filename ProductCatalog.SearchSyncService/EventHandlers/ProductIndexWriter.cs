using Elastic.Clients.Elasticsearch;
using ProductCatalog.Search;

namespace ProductCatalog.SearchSyncService.EventHandlers;

/// <summary>
/// Writes a product document to Elasticsearch.
/// The document id is always the product id, so re-delivering the same event
/// overwrites the existing document instead of creating a duplicate.
/// </summary>
internal class ProductIndexWriter(ElasticsearchClient client, ILogger<ProductIndexWriter> logger)
{
    public async Task IndexAsync(ProductIndexDocument doc, CancellationToken cancellationToken)
    {
        logger.LogInformation("Indexing product {ProductId} to Elasticsearch with {VariantCount} variants",
            doc.ProductId, doc.Variants?.Count ?? 0);

        var response = await client.IndexAsync(doc, i => i.Id(doc.ProductId.ToString()), cancellationToken);

        if (!response.IsValidResponse)
        {
            // Must throw: the caller treats a normal return as success and commits
            // the Kafka offset, which would silently drop the product from the index.
            throw new InvalidOperationException(
                $"Elasticsearch rejected the document for product {doc.ProductId}: {response.ElasticsearchServerError}");
        }

        logger.LogInformation("Successfully indexed product {ProductId}", doc.ProductId);
    }
}
