using ProductCatalog.OutboxService;
using ProductCatalog.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<TransactionalOutboxLogTailingService>();

var host = builder.Build();
host.Run();
