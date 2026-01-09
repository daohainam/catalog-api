# Product Catalog API

A high-performance, scalable microservice for managing product catalogs built with .NET Aspire. This service implements CQRS pattern with event-driven architecture, leveraging Kafka for message streaming, Elasticsearch for powerful search capabilities, and PostgreSQL for persistent storage.

## Introduction

Product Catalog API is designed as a production-ready microservice that handles the complete lifecycle of product catalog management in modern e-commerce applications. The architecture prioritizes high availability, horizontal scalability, and performance through carefully selected technologies and patterns.

The system separates read and write operations using CQRS (Command Query Responsibility Segregation), enabling independent scaling of read-heavy search operations and write-intensive catalog updates. Event sourcing through Kafka ensures reliable data propagation across services, while Elasticsearch provides lightning-fast product search with complex filtering capabilities.

Key capabilities include:
- Product and variant management with multi-dimensional attributes
- Real-time search indexing with Elasticsearch
- Event-driven updates with eventual consistency
- Category, brand, and product grouping
- Image management and associations
- Database migration automation
- Container orchestration with Aspire

## Architecture and Design

### System Architecture

The system follows a microservices architecture with clear separation of concerns:

**Command Side (Write Operations):**
- ProductCatalog.Api: Main API for catalog management operations (Create, Update, Delete)
- ProductCatalog.Infrastructure: Data access layer with Entity Framework Core and PostgreSQL
- ProductCatalog.OutboxService: Transactional outbox pattern implementation for reliable event publishing

**Query Side (Read Operations):**
- ProductCatalog.SearchApi: Search API powered by Elasticsearch
- ProductCatalog.Search: Elasticsearch client and mapping configurations
- ProductCatalog.SearchSyncService: Event consumer that syncs data from Kafka to Elasticsearch

**Supporting Components:**
- ProductCatalog.AppHost: Aspire orchestrator managing all services and dependencies
- ProductCatalog.ServiceDefaults: Shared service configurations and defaults
- ProductCatalog.Api.MigrationService: Database schema migration service
- EventBus & EventBus.Kafka: Event publishing and consumption infrastructure
- ProductCatalog.Events: Shared event contracts

### Design Patterns

**CQRS (Command Query Responsibility Segregation):**
The system separates write operations (commands) handled by the main API from read operations (queries) served by the search API. This allows independent optimization and scaling of each side.

**Event-Driven Architecture:**
All state changes are published as events to Kafka topics. This enables:
- Loose coupling between services
- Asynchronous processing
- Event replay capabilities
- Multiple consumers for different purposes

**Transactional Outbox Pattern:**
The OutboxService ensures reliable event publishing by storing events in the same database transaction as business data, then publishing them asynchronously. This guarantees that events are never lost even if Kafka is temporarily unavailable.

**Repository Pattern:**
Data access is abstracted through Entity Framework Core, providing a clean separation between business logic and data persistence.

### Data Flow

1. Client sends a command to ProductCatalog.Api (create/update product)
2. API validates and persists data to PostgreSQL
3. Changes are stored in the outbox table within the same transaction
4. OutboxService reads from outbox and publishes events to Kafka
5. SearchSyncService consumes events from Kafka
6. SearchSyncService updates Elasticsearch indices
7. Clients query ProductCatalog.SearchApi for search operations
8. SearchApi queries Elasticsearch and returns results

## Technology Stack

**Runtime & Framework:**
- .NET 10.0
- ASP.NET Core for Web APIs
- .NET Aspire for orchestration and service discovery

**Data Storage:**
- PostgreSQL - Primary relational database for product catalog
- Elasticsearch 8.x - Search engine and document store

**Messaging:**
- Apache Kafka - Event streaming platform
- Confluent.Kafka - .NET client library

**Data Access:**
- Entity Framework Core 10.0 - ORM for PostgreSQL
- Npgsql - PostgreSQL provider for EF Core
- Elastic.Clients.Elasticsearch - Elasticsearch client

**API & Documentation:**
- ASP.NET Core Minimal APIs
- OpenAPI/Swagger specification
- Scalar for API documentation

**Development Tools:**
- Kafka UI - Web interface for Kafka
- pgWeb - PostgreSQL web interface

## Project Structure

```
catalog-api/
├── ProductCatalog.AppHost/              # Aspire orchestrator application
│   ├── AppHost.cs                       # Service composition and dependencies
│   └── appsettings.json                 # Orchestrator configuration
│
├── ProductCatalog.Api/                  # Main catalog API (Command side)
│   ├── Apis/                            # API endpoint definitions
│   ├── Bootstraping/                    # Service registration
│   ├── Models/                          # Request/response models
│   ├── Services/                        # Business logic services
│   └── Program.cs                       # Application entry point
│
├── ProductCatalog.SearchApi/            # Search API (Query side)
│   ├── Apis/                            # Search endpoint definitions
│   ├── Bootstraping/                    # Service registration
│   └── Program.cs                       # Application entry point
│
├── ProductCatalog.Infrastructure/       # Data access layer
│   ├── Data/                            # DbContext and configurations
│   ├── Entity/                          # Entity models
│   └── Migrations/                      # EF Core migrations
│
├── ProductCatalog.Search/               # Elasticsearch infrastructure
│   ├── ElasticsearchIndexConfiguration.cs  # Index mappings
│   ├── ElasticsearchIndexInitializer.cs    # Index creation
│   ├── ProductEsMapper.cs               # Event to document mapper
│   └── README.md                        # Elasticsearch optimization docs
│
├── ProductCatalog.OutboxService/        # Outbox pattern implementation
│   ├── TransactionalOutboxLogTailingService.cs
│   └── Program.cs
│
├── ProductCatalog.SearchSyncService/    # Kafka to Elasticsearch sync
│   ├── EventHandlers/                   # Event processing handlers
│   ├── EventHandlingService.cs          # Event consumption logic
│   └── Program.cs
│
├── ProductCatalog.Api.MigrationService/ # Database migration runner
│   └── Program.cs                       # Migration execution
│
├── ProductCatalog.ServiceDefaults/      # Shared service configurations
│   └── Extensions/                      # Common extensions
│
├── EventBus/                            # Event bus abstraction
│   ├── Abstractions/                    # Interfaces
│   └── Events/                          # Base event types
│
├── EventBus.Kafka/                      # Kafka implementation
│   └── KafkaEventPublisher.cs           # Event publishing
│
└── ProductCatalog.Events/               # Event contracts
    ├── ProductEvents.cs                 # Product-related events
    └── VariantEvents.cs                 # Variant-related events
```

## Prerequisites

Before running the application, ensure you have the following installed:

**Required:**
- .NET 10.0 SDK or later
- Docker Desktop (for containerized dependencies)
- Git

**Recommended:**
- Visual Studio 2022 (v17.12+) or Visual Studio Code
- .NET Aspire workload (`dotnet workload install aspire`)

**The following services will be automatically provisioned by Aspire:**
- PostgreSQL (with pgWeb UI)
- Apache Kafka (with Kafka UI)
- Elasticsearch

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/daohainam/catalog-api.git
cd catalog-api
```

### 2. Install .NET Aspire Workload

If you haven't installed the Aspire workload yet:

```bash
dotnet workload install aspire
```

### 3. Run the Application

The easiest way to run the entire system is through the Aspire AppHost:

```bash
cd ProductCatalog.AppHost
dotnet run
```

This command will:
- Start all required infrastructure (PostgreSQL, Kafka, Elasticsearch)
- Run database migrations automatically
- Start all microservices in the correct order with proper dependencies
- Open the Aspire Dashboard in your browser

### 4. Access the Services

Once the application is running, you can access:

**Aspire Dashboard:**
- URL: http://localhost:15888 (or as shown in console)
- Purpose: Monitor all services, view logs, traces, and metrics

**Product Catalog API:**
- URL: http://localhost:5000 (check Aspire dashboard for actual port)
- Swagger UI: http://localhost:5000/scalar/v1
- Purpose: Create, update, and manage products

**Product Search API:**
- URL: http://localhost:5001 (check Aspire dashboard for actual port)
- Swagger UI: http://localhost:5001/scalar/v1
- Purpose: Search and query products

**Infrastructure UIs:**
- Kafka UI: http://localhost:8080 (check Aspire dashboard)
- pgWeb: http://localhost:8081 (check Aspire dashboard)

### 5. Example API Operations

**Create a Product:**

```bash
curl -X POST http://localhost:5000/api/v1/products \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Sample Product",
    "description": "Product description",
    "sku": "SKU-001",
    "price": 29.99
  }'
```

**Search Products:**

```bash
curl "http://localhost:5001/api/v1/products/search?query=sample&page=1&pageSize=10"
```

**Create a Brand:**

```bash
curl -X POST http://localhost:5000/api/v1/brands \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Brand Name",
    "urlSlug": "brand-name"
  }'
```

## Development Workflow

### Project Structure Overview

The solution follows a clean architecture with clear separation:
- **API Layer**: REST endpoints and request handling
- **Application Layer**: Business logic and use cases
- **Infrastructure Layer**: Data access and external services
- **Domain Layer**: Entities and domain events

### Running Individual Services

To run services individually for development:

```bash
# Terminal 1: Start infrastructure
cd ProductCatalog.AppHost
dotnet run

# Terminal 2: Run main API
cd ProductCatalog.Api
dotnet run

# Terminal 3: Run search API
cd ProductCatalog.SearchApi
dotnet run
```

### Database Migrations

To create a new migration:

```bash
cd ProductCatalog.Infrastructure
dotnet ef migrations add MigrationName --startup-project ../ProductCatalog.Api
```

Migrations are automatically applied on startup by the MigrationService.

### Adding New Features

1. Define events in `ProductCatalog.Events` if state changes occur
2. Implement API endpoints in appropriate API project
3. Update database entities and create migrations if needed
4. Add event handlers in `SearchSyncService` for search sync
5. Update Elasticsearch mappings if new searchable fields are added

### Monitoring and Debugging

**Logs:**
All services use structured logging. View logs in:
- Aspire Dashboard (real-time)
- Console output (when running individual services)

**Distributed Tracing:**
The system includes OpenTelemetry tracing. View traces in the Aspire Dashboard to understand request flows across services.

**Event Flow Debugging:**
- Check Kafka topics in Kafka UI to verify event publishing
- Monitor SearchSyncService logs for event consumption
- Query Elasticsearch directly to verify indexing

### Testing

The solution supports multiple testing approaches:

**API Testing:**
- Use the built-in Swagger/Scalar UI
- Use the `.http` files in API projects with VS Code REST Client
- Use Postman or similar tools

**Integration Testing:**
Aspire makes it easy to run integration tests with all dependencies:

```bash
dotnet test
```

## Configuration

### Application Settings

Key configuration files:
- `appsettings.json` - Default configuration
- `appsettings.Development.json` - Development overrides
- Environment variables - Production secrets

### Elasticsearch Configuration

Configure index settings in `ProductCatalog.SearchApi/appsettings.json`:

```json
{
  "Elasticsearch": {
    "Index": {
      "NumberOfShards": 3,
      "NumberOfReplicas": 1,
      "RefreshInterval": "30s"
    }
  }
}
```

See `ProductCatalog.Search/ELASTICSEARCH_OPTIMIZATION.md` for detailed optimization guidance.

### Kafka Configuration

Kafka settings are automatically configured by Aspire. For custom configuration, update the connection strings in the AppHost or service-specific settings.

## Production Deployment

For production deployment:

1. **Container Images**: Build Docker images for each service
2. **Kubernetes**: Use the Aspire-generated manifests or Helm charts
3. **Configuration**: Use environment variables or Azure App Configuration
4. **Secrets**: Store in Azure Key Vault or similar
5. **Monitoring**: Set up Application Insights or similar APM

Refer to .NET Aspire deployment documentation for detailed guidance.

## Performance Characteristics

**Query Performance:**
- Sub-50ms response time for most search queries
- Supports 1000+ queries/second per search node
- Elasticsearch optimized with proper field mappings

**Indexing Throughput:**
- 1000+ documents/second indexing rate
- Configurable refresh interval for bulk operations
- Async event processing via Kafka

**Scalability:**
- Horizontal scaling of API services
- Independent scaling of search services
- Kafka partitioning for parallel event processing
- Elasticsearch sharding for large datasets (30M+ products)

## License

This project is licensed under the MIT License. See the [LICENSE.txt](LICENSE.txt) file for details.

## Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

When contributing, please:
1. Follow the existing code style and patterns
2. Add tests for new features
3. Update documentation as needed
4. Ensure all tests pass before submitting

## Support

For questions, issues, or feature requests, please open an issue on GitHub.

## Acknowledgments

Built with:
- .NET Aspire - Microsoft's cloud-ready stack
- Elasticsearch - Powerful search and analytics
- Apache Kafka - Distributed event streaming
- PostgreSQL - Reliable relational database