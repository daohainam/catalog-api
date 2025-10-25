using ProductCatalog.Api.Apis;
using ProductCatalog.Api.Bootstraping;
using ProductCatalog.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApplicationServices();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapCatalogApi();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.DefaultFonts = false;
    });
    app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
}

app.Run();
