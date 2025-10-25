using ProductCatalog.SearchApi.Apis;
using ProductCatalog.SearchApi.Bootstraping;
using ProductCatalog.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddApplicationServices();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapSearchApi();

app.Run();