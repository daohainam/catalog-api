using Confluent.Kafka;
using Elastic.Clients.Elasticsearch;
using EventBus.Events;
using ProductCatalog.Events;
using ProductCatalog.Search;
using System.Text.Json;

namespace ProductCatalog.SearchSyncService.EventHandlers;
internal class ProductCreatedEventHandler(ElasticsearchClient client, ILogger<ProductCreatedEventHandler> logger): IEventHandler
{
    public async Task HandleAsync(IntegrationEvent evt, CancellationToken cancellationToken)
    { 
        if (evt is not ProductCreatedEvent productCreatedEvent)
        {
            logger.LogError("Invalid event type: {t}", evt.GetType().FullName);
            return;
        }

        try
        {
            var doc = ProductEsMapper.Map(productCreatedEvent);

            // serialize doc to json string is not necessary, just for logging purpose, should be removed in production
            logger.LogInformation("Indexing product {id} to Elasticsearch, document: {doc}", productCreatedEvent.ProductId, JsonSerializer.Serialize(doc));
            var response = await client.IndexAsync(doc, cancellationToken: cancellationToken);

            if (!response.IsValidResponse)
            {
                logger.LogInformation("Error indexing product {id}, {err}", productCreatedEvent.ProductId, response.ElasticsearchServerError);
            }
            else
            {
                logger.LogInformation("Successfully indexed product {id}", productCreatedEvent.ProductId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error indexing product {id}", productCreatedEvent.ProductId);
            throw;
        }
    }           
}