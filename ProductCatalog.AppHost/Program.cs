var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
    .WithImageTag("latest");
var postgres = builder.AddPostgres("postgres")
    .WithImageTag("latest")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin((rbuilder) => {
        rbuilder
            .WithImageTag("latest");
    });

var catalogDb = postgres.AddDatabase("catalogdb");

var migrationService = builder.AddProject<Projects.ProductCatalog_Api_MigrationService>("catalog-api-migrationservice").WithReference(catalogDb).WaitFor(catalogDb);

builder.AddProject<Projects.ProductCatalog_Api>("catalog-api").WithReference(catalogDb).WaitForCompletion(migrationService);

builder.Build().Run();
