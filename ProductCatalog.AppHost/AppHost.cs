var builder = DistributedApplication.CreateBuilder(args);

//var redis = builder.AddRedis("redis")
//    .WithImageTag("latest");
var kafka = builder.AddKafka("kafka")
    .WithLifetime(ContainerLifetime.Persistent).WithDataVolume()
    .WithKafkaUI();
var postgres = builder.AddPostgres("postgresql")
    .WithLifetime(ContainerLifetime.Persistent).WithDataVolume()
    .WithPgWeb();
var elasticsearch = builder.AddElasticsearch("elasticsearch")
    .WithLifetime(ContainerLifetime.Persistent).WithDataVolume().WithContainerRuntimeArgs("--memory=512m");

//var postgres = builder.AddPostgres("postgres")
//    .WithImageTag("latest")
//    .WithVolume("catalogdb", "/var/lib/postgresql/data")
//    .WithLifetime(ContainerLifetime.Persistent)
//    .WithPgAdmin((rbuilder) => {
//        rbuilder
//            .WithImageTag("latest");
//    });

var catalogDb = postgres.AddDatabase("catalogdb", "catalog");

var migrationService = builder.AddProject<Projects.ProductCatalog_Api_MigrationService>("catalog-api-migrationservice")
    .WithReference(catalogDb).WaitFor(catalogDb);

var searchSyncService = builder.AddProject<Projects.ProductCatalog_SearchSyncService>("productcatalog-searchsyncservice")
    .WithReference(kafka)
    .WithReference(elasticsearch)
    .WaitFor(kafka)
    .WaitFor(elasticsearch);

var outboxService = builder.AddProject<Projects.ProductCatalog_OutboxService>("productcatalog-outboxservice")
    .WithReference(catalogDb)
    .WithReference(kafka)
    .WaitFor(kafka)
    .WaitForCompletion(migrationService);

builder.AddProject<Projects.ProductCatalog_Api>("catalog-api")
    .WithReference(catalogDb).WaitFor(outboxService);


builder.Build().Run();
