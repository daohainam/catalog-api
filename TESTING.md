# Testing Guide for Product Catalog API

This document provides information about the test suite for the Product Catalog API, an Aspire-based microservice with CQRS, event-driven architecture, Kafka messaging, Elasticsearch, and PostgreSQL.

## Test Projects

### 1. ProductCatalog.Api.Tests (Unit Tests)
Contains unit tests that test individual components in isolation using in-memory databases and mocked dependencies.

**Test Categories:**
- **ProductEsMapperTests**: Tests for mapping Product entities to Elasticsearch documents
- **BrandServiceTests**: Tests for Brand CRUD operations
- **ProductServiceTests**: Tests for Product CRUD operations with variants and dimensions

**Key Tests:**
- Product to Elasticsearch document mapping
- Price calculation (minimum price selection)
- Category path building
- Variant and dimension handling
- Pagination logic
- CRUD operations

### 2. ProductCatalog.IntegrationTests (Integration Tests)
Contains integration tests that test the entire system using Aspire.Hosting.Testing with real infrastructure (PostgreSQL, Kafka, Elasticsearch).

**Test Categories:**
- **CatalogApiIntegrationTests**: Tests for basic API operations
- **ProductApiWithElasticsearchTests**: Tests for Product API with Elasticsearch synchronization

**Key Tests:**
- Brand API CRUD with real database
- Category API operations
- Dimension API operations
- Product creation with Kafka event publishing
- Elasticsearch synchronization
- Multi-variant product handling
- Pagination with real data

## Prerequisites

Before running the tests, ensure you have:

1. **.NET 10 SDK** installed
2. **Docker** installed and running (for integration tests)
3. All NuGet packages restored:
   ```bash
   dotnet restore
   ```

## Running Tests

### Run All Tests

```bash
cd /path/to/catalog-api
dotnet test
```

### Run Only Unit Tests

```bash
dotnet test ProductCatalog.Api.Tests/ProductCatalog.Api.Tests.csproj
```

### Run Only Integration Tests

```bash
dotnet test ProductCatalog.IntegrationTests/ProductCatalog.IntegrationTests.csproj
```

### Run Tests with Detailed Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run Tests with Code Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Test Structure

### Unit Tests

Unit tests use:
- **xUnit** as the test framework
- **FluentAssertions** for readable assertions
- **EntityFramework InMemory** database for testing repository logic
- **Moq** for mocking dependencies (if needed)

Example:
```csharp
[Fact]
public async Task CreateBrand_ShouldAddBrandToDatabase()
{
    // Arrange
    using var context = CreateInMemoryContext();
    var brand = new Brand { /* ... */ };
    
    // Act
    await context.Brands.AddAsync(brand);
    await context.SaveChangesAsync();
    
    // Assert
    var savedBrand = await context.Brands.FindAsync(brand.Id);
    savedBrand.Should().NotBeNull();
}
```

### Integration Tests

Integration tests use:
- **Aspire.Hosting.Testing** for spinning up the entire application
- **DistributedApplicationTestingBuilder** for creating test environments
- **Real infrastructure** (PostgreSQL, Kafka, Elasticsearch) via Docker containers
- **HttpClient** for testing HTTP APIs
- **ElasticsearchClient** for verifying Elasticsearch synchronization

Example:
```csharp
public async Task InitializeAsync()
{
    _builder = await DistributedApplicationTestingBuilder
        .CreateAsync<Projects.ProductCatalog_AppHost>();
    
    _app = await _builder.BuildAsync();
    await _app.StartAsync();
    
    _httpClient = _app.CreateHttpClient("catalog-api");
}

[Fact]
public async Task CreateProduct_ShouldSyncToElasticsearch()
{
    // Test implementation
}
```

## Test Coverage

The test suite covers:

1. **Business Logic**
   - Product to Elasticsearch document mapping
   - Price calculations
   - Variant management
   - Dimension handling

2. **Data Access**
   - CRUD operations for all entities
   - Pagination
   - Relationships between entities

3. **API Endpoints**
   - Brand endpoints
   - Category endpoints
   - Dimension endpoints
   - Product endpoints
   - Variant endpoints

4. **Integration Scenarios**
   - End-to-end product creation
   - Kafka event publishing
   - Elasticsearch synchronization
   - Multiple service coordination

## Important Notes

### Integration Test Timing

Integration tests that verify Elasticsearch synchronization include delays to allow for:
1. Kafka message publishing
2. SearchSyncService processing
3. Elasticsearch indexing

Default delay is 10 seconds but may need adjustment based on system performance.

### Test Isolation

- Unit tests use in-memory databases with unique database names per test
- Integration tests use Aspire's container orchestration for isolated environments
- Each integration test class creates its own application instance

### Aspire AppHost

Integration tests rely on the Aspire AppHost configuration in `ProductCatalog.AppHost`. Any changes to service configuration should be reflected in tests.

## Troubleshooting

### Docker Issues

If integration tests fail with Docker-related errors:
```bash
docker ps  # Verify Docker is running
docker system prune  # Clean up old containers
```

### Port Conflicts

If you see port binding errors, ensure no other instances of the services are running:
```bash
docker ps
docker stop $(docker ps -aq)  # Stop all containers
```

### Elasticsearch Sync Delays

If Elasticsearch tests fail intermittently, you may need to increase the sync delay in `ProductApiWithElasticsearchTests.cs`:
```csharp
await Task.Delay(TimeSpan.FromSeconds(15));  // Increase from 10 to 15 seconds
```

## Continuous Integration

These tests are designed to run in CI/CD pipelines. Ensure your CI environment has:
- Docker support
- Sufficient memory (at least 4GB recommended)
- Network access for pulling container images

## Contributing

When adding new features:
1. Add unit tests for business logic
2. Add integration tests for end-to-end scenarios
3. Ensure all tests pass before submitting PR
4. Maintain test coverage above 80%

## Test Execution Times

Approximate execution times:
- **Unit Tests**: ~2-3 seconds
- **Integration Tests**: ~30-60 seconds (depends on container startup)

## Additional Resources

- [.NET Testing Documentation](https://docs.microsoft.com/en-us/dotnet/core/testing/)
- [xUnit Documentation](https://xunit.net/)
- [Aspire Testing Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/testing/)
- [FluentAssertions Documentation](https://fluentassertions.com/introduction)
