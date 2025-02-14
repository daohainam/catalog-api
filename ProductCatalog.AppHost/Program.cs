var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
    .WithImageTag("latest");
var postgres = builder.AddPostgres("postgres")
    .WithImageTag("latest")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin();

var catalogDb = postgres.AddDatabase("catalogdb");

builder.AddProject<Projects.ProductCatalog_Api>("productcatalog-api");

builder.Build().Run();
