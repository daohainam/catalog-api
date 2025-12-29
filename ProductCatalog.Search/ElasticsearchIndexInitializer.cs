using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;

namespace ProductCatalog.Search;

/// <summary>
/// Service for initializing and managing Elasticsearch indices.
/// Call this during application startup to ensure the index exists with proper mappings.
/// </summary>
public class ElasticsearchIndexInitializer
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticsearchIndexInitializer> _logger;

    public ElasticsearchIndexInitializer(
        ElasticsearchClient client, 
        ILogger<ElasticsearchIndexInitializer> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Ensures the product index exists with optimized mappings.
    /// If the index doesn't exist, it will be created.
    /// </summary>
    /// <param name="recreateIfExists">If true, deletes and recreates the index if it already exists.</param>
    public async Task InitializeAsync(bool recreateIfExists = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking if Elasticsearch product index exists...");
            
            var existsResponse = await ElasticsearchIndexConfiguration.IndexExistsAsync(_client, cancellationToken);
            
            if (existsResponse.Exists)
            {
                if (recreateIfExists)
                {
                    _logger.LogWarning("Product index exists, deleting and recreating...");
                    var deleteResponse = await ElasticsearchIndexConfiguration.DeleteIndexAsync(_client, cancellationToken);
                    
                    if (!deleteResponse.IsValidResponse)
                    {
                        _logger.LogError("Failed to delete product index: {error}", 
                            deleteResponse.DebugInformation);
                        throw new InvalidOperationException("Failed to delete existing index");
                    }
                    
                    _logger.LogInformation("Product index deleted successfully");
                }
                else
                {
                    _logger.LogInformation("Product index already exists, skipping creation");
                    return;
                }
            }
            
            _logger.LogInformation("Creating Elasticsearch product index with optimized mappings...");
            
            var createResponse = await ElasticsearchIndexConfiguration.CreateProductIndexAsync(_client, cancellationToken);
            
            if (createResponse.IsValidResponse)
            {
                _logger.LogInformation("Product index created successfully with optimized mappings");
                _logger.LogInformation("Index settings: 3 shards, 1 replica, 30s refresh interval");
                _logger.LogInformation("Variants configured as nested type for complex queries");
                _logger.LogInformation("Prices stored as scaled_float for efficient storage");
            }
            else
            {
                _logger.LogError("Failed to create product index: {error}", 
                    createResponse.DebugInformation);
                throw new InvalidOperationException("Failed to create index with optimized mappings");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Elasticsearch product index");
            throw;
        }
    }

    /// <summary>
    /// Checks the health of the Elasticsearch connection.
    /// </summary>
    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var pingResponse = await _client.PingAsync(cancellationToken);
            return pingResponse.IsValidResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ping Elasticsearch");
            return false;
        }
    }
}
