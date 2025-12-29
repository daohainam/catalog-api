using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

namespace ProductCatalog.Search;

/// <summary>
/// Configuration options for Elasticsearch product index.
/// </summary>
public class ElasticsearchIndexOptions
{
    /// <summary>
    /// Number of primary shards for the index. Default is 3.
    /// Increase for larger datasets (each shard should be 20-50GB max).
    /// </summary>
    public int NumberOfShards { get; set; } = 3;

    /// <summary>
    /// Number of replica shards. Default is 1.
    /// Increase for higher read throughput and better availability.
    /// </summary>
    public int NumberOfReplicas { get; set; } = 1;

    /// <summary>
    /// How often the index should be refreshed. Default is "30s".
    /// Lower values = more real-time search but slower indexing.
    /// Higher values = faster indexing but less real-time search.
    /// </summary>
    public string RefreshInterval { get; set; } = "30s";
}

/// <summary>
/// Elasticsearch index configuration optimized for high-volume product catalog queries.
/// This provides explicit mapping configuration to optimize storage and query performance.
/// </summary>
public static class ElasticsearchIndexConfiguration
{
    public const string IndexName = "productindexdocument";

    /// <summary>
    /// Creates the product index with optimized settings and mappings.
    /// Key optimizations:
    /// - Keyword types for exact match fields (IDs, SKUs, slugs) - faster filtering
    /// - Nested type for variants array - enables complex variant filtering
    /// - Scaled float for prices - more efficient storage
    /// - Disabled indexing for display-only fields - reduces index size
    /// - Text + keyword multi-field for sortable search fields
    /// </summary>
    public static async Task<CreateIndexResponse> CreateProductIndexAsync(
        ElasticsearchClient client,
        ElasticsearchIndexOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ElasticsearchIndexOptions();

        var createIndexRequest = new CreateIndexRequest(IndexName)
        {
            Settings = new IndexSettings
            {
                NumberOfShards = options.NumberOfShards,
                NumberOfReplicas = options.NumberOfReplicas,
                RefreshInterval = new Elastic.Clients.Elasticsearch.Duration(options.RefreshInterval)
            },
            Mappings = new TypeMapping
            {
                Properties = new Properties
                {
                    // Product identifiers - keyword for exact matching (no analysis)
                    { "product_id", new KeywordProperty() },
                    { "slug", new KeywordProperty() },
                    
                    // Searchable text with keyword subfield for sorting/aggregations
                    { "name", new TextProperty 
                        { 
                            Fields = new Properties
                            {
                                { "keyword", new KeywordProperty { IgnoreAbove = 256 } }
                            }
                        } 
                    },
                    { "description", new TextProperty() },
                    
                    // Brand
                    { "brand_id", new KeywordProperty() },
                    { "brand_name", new TextProperty 
                        { 
                            Fields = new Properties { { "keyword", new KeywordProperty() } }
                        } 
                    },
                    
                    // Category - keywords for fast filtering
                    { "category_id", new KeywordProperty() },
                    { "category_name", new TextProperty 
                        { 
                            Fields = new Properties { { "keyword", new KeywordProperty() } }
                        } 
                    },
                    { "category_slug", new KeywordProperty() },
                    { "category_path", new KeywordProperty() }, // For hierarchical category filtering
                    
                    // Groups - array of keywords
                    { "group_ids", new KeywordProperty() },
                    { "group_names", new KeywordProperty() },
                    
                    // Images - object with disabled indexing for performance
                    { "images", new ObjectProperty 
                        {
                            Properties = new Properties
                            {
                                { "url", new KeywordProperty { Index = false } },
                                { "alt", new TextProperty { Index = false } },
                                { "sort_order", new IntegerNumberProperty { Index = false } }
                            }
                        }
                    },
                    
                    // Rollup fields - optimized for aggregations and filtering
                    { "price_min", new ScaledFloatNumberProperty { ScalingFactor = 100 } }, // 2 decimal places
                    { "in_stock", new BooleanProperty() },
                    { "variant_count", new IntegerNumberProperty { Index = false } }, // Display only
                    
                    // Dimensions - for faceting
                    { "dimensions", new ObjectProperty
                        {
                            Properties = new Properties
                            {
                                { "dimension_id", new KeywordProperty() },
                                { "name", new KeywordProperty() },
                                { "display_type", new KeywordProperty() }
                            }
                        }
                    },
                    
                    // Variants - NESTED for proper array querying
                    { "variants", new NestedProperty
                        {
                            Properties = new Properties
                            {
                                { "variant_id", new KeywordProperty() },
                                { "sku", new KeywordProperty() },
                                { "barcode", new KeywordProperty() },
                                { "price", new ScaledFloatNumberProperty { ScalingFactor = 100 } },
                                { "in_stock", new BooleanProperty() },
                                { "is_active", new BooleanProperty() },
                                { "created_at", new DateProperty { Index = false } },
                                { "updated_at", new DateProperty() },
                                { "description", new TextProperty { Index = false } },
                                
                                // Variant dimensions - nested for complex queries
                                { "dimensions", new NestedProperty
                                    {
                                        Properties = new Properties
                                        {
                                            { "dimension_id", new KeywordProperty() },
                                            { "value", new KeywordProperty() },
                                            { "display_value", new KeywordProperty { Index = false } }
                                        }
                                    }
                                },
                                
                                // Flattened for simple key-value queries
                                { "dims_flat", new FlattenedProperty() },
                                
                                // Variant images
                                { "images", new ObjectProperty
                                    {
                                        Properties = new Properties
                                        {
                                            { "url", new KeywordProperty { Index = false } },
                                            { "alt", new TextProperty { Index = false } },
                                            { "sort_order", new IntegerNumberProperty { Index = false } }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    
                    // Primary variant for quick access
                    { "primary_variant", new ObjectProperty
                        {
                            Properties = new Properties
                            {
                                { "variant_id", new KeywordProperty() },
                                { "price", new ScaledFloatNumberProperty { ScalingFactor = 100 } },
                                { "in_stock", new BooleanProperty() }
                            }
                        }
                    },
                    
                    // Status and timestamps
                    { "is_active", new BooleanProperty() },
                    { "created_at", new DateProperty { Index = false } },
                    { "updated_at", new DateProperty() },
                    
                    // Completion suggester for autocomplete
                    { "suggest", new CompletionProperty() }
                }
            }
        };

        return await client.Indices.CreateAsync(createIndexRequest, cancellationToken);
    }

    /// <summary>
    /// Checks if the product index exists.
    /// </summary>
    public static async Task<Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse> IndexExistsAsync(
        ElasticsearchClient client, 
        CancellationToken cancellationToken = default)
    {
        return await client.Indices.ExistsAsync(IndexName, cancellationToken);
    }

    /// <summary>
    /// Deletes the product index if it exists.
    /// </summary>
    public static async Task<DeleteIndexResponse> DeleteIndexAsync(
        ElasticsearchClient client, 
        CancellationToken cancellationToken = default)
    {
        return await client.Indices.DeleteAsync(IndexName, cancellationToken);
    }
}
