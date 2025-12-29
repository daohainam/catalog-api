# Elasticsearch Indexing Optimization

This document describes the Elasticsearch indexing optimizations implemented for the ProductCatalog search functionality to handle high volumes of requests efficiently.

## Overview

The product catalog uses Elasticsearch for full-text search and filtering capabilities. The optimizations focus on:
- Efficient data structure design
- Proper field type mappings
- Index settings tuned for high query throughput
- Reduced index size through selective field indexing

## Key Optimizations

### 1. Field Type Mappings

#### Keyword vs Text Fields
- **Keyword fields** are used for exact-match filtering, sorting, and aggregations:
  - Product IDs, SKUs, barcodes
  - Category slugs, paths
  - Brand IDs, group IDs
  - Dimension IDs and values
  
- **Text fields with keyword subfield** for search + sort/filter capabilities:
  - Product names, brand names, category names
  - The `keyword` subfield allows sorting and aggregations without re-indexing

#### Numeric Field Optimization
- **Scaled Float (scaling factor 100)** for prices:
  - More efficient than regular floats for currency values
  - Stores prices with 2 decimal places precision
  - Reduces storage and memory footprint
  
- **Integer** for counts (variant_count)
  - Appropriate type for whole numbers

#### Nested Type for Variants
- Variants are stored as **nested objects** instead of flattened arrays
- Enables complex queries like "find products with a red, size L variant in stock"
- Preserves the relationship between variant fields during queries
- Example query: Find products with variants that have specific dimension combinations

### 2. Index-Level Optimizations

#### Disabled Indexing for Display-Only Fields
Fields that are only used for display (not search/filter) have `Index = false`:
- Image URLs and alt text
- Variant descriptions
- Creation timestamps
- Sort orders

Benefits:
- Reduced index size (up to 30-40% savings)
- Faster indexing operations
- Lower memory usage

#### Field Data Configuration
- Text fields use `doc_values` by default for efficient aggregations
- Keyword fields optimized for both term queries and aggregations

### 3. Index Settings

```csharp
Settings:
  NumberOfShards: 3        // Distribute load across multiple shards
  NumberOfReplicas: 1      // High availability with one replica
  RefreshInterval: 30s     // Balance between indexing speed and search visibility
```

#### Shard Configuration
- **3 shards**: Distributes the load across multiple nodes for better query parallelization
- Each shard can be queried independently, increasing throughput
- Suitable for medium to large product catalogs (up to ~30M products)

#### Replica Configuration
- **1 replica**: Provides high availability and doubles read capacity
- Queries can be distributed across primary and replica shards

#### Refresh Interval
- **30 seconds**: Optimizes for indexing throughput over near-real-time search
- Products become searchable within 30 seconds of indexing
- Can be adjusted based on requirements:
  - Decrease for more real-time updates (at cost of indexing performance)
  - Increase for higher indexing throughput (for bulk operations)

### 4. Data Structure Design

#### Flattened Dimensions (dims_flat)
- Provides simple key-value querying for variant dimensions
- Useful for faceted search without nested queries
- Example: `dims_flat.color: "red"` is simpler than nested queries

#### Primary Variant
- Denormalized for quick access to main product display data
- Avoids nested queries for common use cases
- Contains: price, in_stock status, variant_id

#### Completion Suggester
- Configured for autocomplete functionality
- Input includes product name and brand name
- Enables fast type-ahead search

## Query Performance Tips

### 1. Use Nested Queries for Variants
When filtering by variant properties, use nested queries:

```csharp
Query = q => q.Nested(n => n
    .Path("variants")
    .Query(nq => nq.Bool(b => b
        .Must(
            m => m.Term(t => t.Field("variants.in_stock").Value(true)),
            m => m.Range(r => r.Field("variants.price").Lte(100))
        )
    ))
)
```

### 2. Use Keyword Subfields for Sorting
For sortable text fields, use the `.keyword` subfield:

```csharp
Sort = s => s.Field("name.keyword", new FieldSort { Order = SortOrder.Asc })
```

### 3. Filter Before Aggregating
Use filter context for boolean queries (faster than query context):

```csharp
Query = q => q.Bool(b => b
    .Filter(
        f => f.Term(t => t.Field("category_id").Value(categoryId)),
        f => f.Term(t => t.Field("is_active").Value(true))
    )
)
```

### 4. Use Term Queries for Keywords
For keyword fields, use term queries instead of match queries:

```csharp
Query = q => q.Term(t => t.Field("brand_id").Value(brandId))
```

## Monitoring and Tuning

### Index Size Estimation
- Average product document: ~5-10KB (depending on variant count)
- 100K products ≈ 500MB-1GB index size
- Monitor actual size and adjust shard count if needed

### Performance Metrics to Track
1. **Query latency**: Should be < 50ms for most queries
2. **Indexing throughput**: Should handle 1000+ docs/second
3. **Heap memory usage**: Should stay below 75% of available heap
4. **Query rate**: Track requests per second per shard

### When to Scale
- If query latency increases: Add more replicas
- If indexing is slow: Increase refresh_interval temporarily
- If single shard > 50GB: Increase shard count (requires reindex)
- If CPU is bottleneck: Add more nodes

## Index Management

### Creating the Index
The index is automatically created on application startup with optimized mappings:

```csharp
var initializer = serviceProvider.GetRequiredService<ElasticsearchIndexInitializer>();
await initializer.InitializeAsync(recreateIfExists: false);
```

### Recreating the Index
To recreate with new mappings (during development):

```csharp
await initializer.InitializeAsync(recreateIfExists: true);
```

### Index Aliases
For production, consider using index aliases:
- Create new index with timestamp: `productindexdocument_20250101`
- Reindex data to new index
- Switch alias atomically to new index
- Delete old index

This enables zero-downtime mapping changes.

## Best Practices

1. **Always use the provided ElasticsearchIndexConfiguration** to ensure consistent mappings
2. **Test queries on production-like data volumes** to validate performance
3. **Monitor index size and query performance** regularly
4. **Use bulk operations** for indexing multiple documents
5. **Consider warm/cold architecture** for historical data
6. **Implement circuit breakers** for Elasticsearch failures
7. **Use request caching** for frequently executed queries

## Common Query Patterns

### Full-Text Search
```csharp
Query = q => q.MultiMatch(mm => mm
    .Query(searchTerm)
    .Fields(f => f
        .Field("name", 2.0)      // Boost name matches
        .Field("description")
        .Field("brand_name")
    )
)
```

### Faceted Search with Aggregations
```csharp
Aggregations = a => a
    .Terms("brands", t => t.Field("brand_id"))
    .Terms("categories", t => t.Field("category_id"))
    .Range("price_ranges", r => r
        .Field("price_min")
        .Ranges(
            new AggregationRange { To = 50 },
            new AggregationRange { From = 50, To = 100 },
            new AggregationRange { From = 100 }
        )
    )
```

### In-Stock Products with Price Filter
```csharp
Query = q => q.Bool(b => b
    .Filter(
        f => f.Term(t => t.Field("in_stock").Value(true)),
        f => f.Range(r => r.Field("price_min").Lte(maxPrice))
    )
)
```

## Troubleshooting

### Slow Queries
1. Check if query is using nested queries correctly
2. Verify that filters are in filter context (not query context)
3. Review field types - ensure using keyword for exact matches
4. Check if sort fields use `.keyword` subfield

### High Memory Usage
1. Verify that display-only fields have `Index = false`
2. Check field data usage on text fields
3. Consider reducing number of replicas temporarily
4. Clear field data cache if necessary

### Indexing Failures
1. Check document size (max 100MB by default)
2. Verify field mappings match ProductIndexDocument structure
3. Check Elasticsearch cluster health
4. Review refresh_interval setting

## Future Enhancements

Potential optimizations for even higher volumes:

1. **Index Sorting**: Pre-sort by common query patterns (e.g., price, popularity)
2. **Custom Analyzers**: Language-specific analyzers for better search
3. **Percolator Queries**: For real-time alerting on new products
4. **Vector Search**: For similar product recommendations
5. **Index Lifecycle Management**: Automatic archival of old products
6. **Cross-cluster Replication**: For geo-distributed deployments
