using ProductCatalog.Api.Bootstraping;
using ProductCatalog.Api.MigrationService;
using ProductCatalog.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<Worker>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

builder.AddApplicationServices();

var host = builder.Build();
host.Run();
