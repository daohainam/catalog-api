using ProductCatalog.Api.Apis;
using ProductCatalog.Api.Bootstraping;
using ProductCatalog.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApplicationServices();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
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
