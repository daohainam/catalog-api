using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProductCatalog.Api;
using ProductCatalog.Api.Apis;
using ProductCatalog.Api.Models;
using ProductCatalog.Api.Services;
using ProductCatalog.Infrastructure.Data;

namespace FunctionalTests.Tests
{
    public class ProductCatalogApiTests : IClassFixture<ProductCatalogApiFixture>
    {
        private readonly ProductCatalogApiFixture _fixture;
        public ProductCatalogApiTests(ProductCatalogApiFixture fixture)
        {
            _fixture = fixture;
        }

        // Instructions:
        // 1. Add a project reference to the target AppHost project, e.g.:
        //
        //    <ItemGroup>
        //        <ProjectReference Include="../MyAspireApp.AppHost/MyAspireApp.AppHost.csproj" />
        //    </ItemGroup>
        //
        // 2. Uncomment the following example test and update 'Projects.MyAspireApp_AppHost' to match your AppHost project:
        //
        //[Fact]
        //public async Task GetWebResourceRootReturnsOkStatusCode()
        //{
        //    // Arrange
        //    var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.ProductCatalog_Api>();
        //    appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        //    {
        //        clientBuilder.AddStandardResilienceHandler();
        //    });
        //    // To output logs to the xUnit.net ITestOutputHelper, consider adding a package from https://www.nuget.org/packages?q=xunit+logging

        //    await using var app = await appHost.BuildAsync();
        //    var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        //    await app.StartAsync();

        //    // Act
        //    var httpClient = app.CreateHttpClient("webfrontend");
        //    await resourceNotificationService.WaitForResourceAsync("webfrontend", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));
        //    var response = await httpClient.GetAsync("/");

        //    // Assert
        //    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        //}

        private CatalogServices GetCatalogServices()
        {
            //_fixture.DbConnectionString = _fixture.DbConnectionString.Replace("localhost", "host.docker.internal");

            var mapperConfiguration = new MapperConfiguration(cfg => {
                cfg.AddProfile<ModelProfile>();
            });

            return new CatalogServices(_fixture.DbContext, mapperConfiguration.CreateMapper(), NullLogger<CatalogServices>.Instance);
        }

        private CatalogServices? catalogServices;
        private CatalogServices CatalogServices => catalogServices ??= GetCatalogServices();

        [Fact]
        public async Task Create_Category_And_Read_Success()
        {
            // Arrange
            var category = new CategoryCreate(Guid.Empty, "Test Category", "Test Description");

            // Act
            var result = await CatalogApi.CreateCategory(CatalogServices, category);

            // Assert
            Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        }
    }
}
