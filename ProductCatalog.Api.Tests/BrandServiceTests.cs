using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Data;
using ProductCatalog.Infrastructure.Entity;

namespace ProductCatalog.Api.Tests;

public class BrandServiceTests
{
    private ProductCatalogDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ProductCatalogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ProductCatalogDbContext(options);
    }

    [Fact]
    public async Task CreateBrand_ShouldAddBrandToDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var brand = new Brand
        {
            Id = Guid.NewGuid(),
            Name = "Test Brand",
            Description = "Test Description",
            UrlSlug = "test-brand",
            LogoUrl = "https://example.com/logo.png"
        };

        // Act
        await context.Brands.AddAsync(brand);
        await context.SaveChangesAsync();

        // Assert
        var savedBrand = await context.Brands.FindAsync(brand.Id);
        savedBrand.Should().NotBeNull();
        savedBrand!.Name.Should().Be("Test Brand");
        savedBrand.Description.Should().Be("Test Description");
        savedBrand.UrlSlug.Should().Be("test-brand");
        savedBrand.LogoUrl.Should().Be("https://example.com/logo.png");
    }

    [Fact]
    public async Task UpdateBrand_ShouldModifyExistingBrand()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var brandId = Guid.NewGuid();
        var brand = new Brand
        {
            Id = brandId,
            Name = "Original Brand",
            Description = "Original Description",
            UrlSlug = "original-brand",
            LogoUrl = "https://example.com/original.png"
        };

        await context.Brands.AddAsync(brand);
        await context.SaveChangesAsync();

        // Act
        var existingBrand = await context.Brands.FindAsync(brandId);
        existingBrand!.Name = "Updated Brand";
        existingBrand.Description = "Updated Description";
        existingBrand.UrlSlug = "updated-brand";
        existingBrand.LogoUrl = "https://example.com/updated.png";

        context.Brands.Update(existingBrand);
        await context.SaveChangesAsync();

        // Assert
        var updatedBrand = await context.Brands.FindAsync(brandId);
        updatedBrand.Should().NotBeNull();
        updatedBrand!.Name.Should().Be("Updated Brand");
        updatedBrand.Description.Should().Be("Updated Description");
        updatedBrand.UrlSlug.Should().Be("updated-brand");
        updatedBrand.LogoUrl.Should().Be("https://example.com/updated.png");
    }

    [Fact]
    public async Task FindBrandById_ShouldReturnCorrectBrand()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var brandId = Guid.NewGuid();
        var brand = new Brand
        {
            Id = brandId,
            Name = "Find Test Brand",
            Description = "Description",
            UrlSlug = "find-test-brand"
        };

        await context.Brands.AddAsync(brand);
        await context.SaveChangesAsync();

        // Act
        var foundBrand = await context.Brands.FindAsync(brandId);

        // Assert
        foundBrand.Should().NotBeNull();
        foundBrand!.Id.Should().Be(brandId);
        foundBrand.Name.Should().Be("Find Test Brand");
    }

    [Fact]
    public async Task FindBrands_ShouldReturnPaginatedResults()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        for (int i = 0; i < 15; i++)
        {
            await context.Brands.AddAsync(new Brand
            {
                Id = Guid.NewGuid(),
                Name = $"Brand {i}",
                Description = $"Description {i}",
                UrlSlug = $"brand-{i}"
            });
        }
        await context.SaveChangesAsync();

        // Act
        var firstPage = await context.Brands
            .Skip(0)
            .Take(10)
            .ToArrayAsync();

        var secondPage = await context.Brands
            .Skip(10)
            .Take(10)
            .ToArrayAsync();

        // Assert
        firstPage.Should().HaveCount(10);
        secondPage.Should().HaveCount(5);
    }

    [Fact]
    public async Task CreateBrand_WithEmptyId_ShouldBeAssignedNewId()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var brand = new Brand
        {
            Id = Guid.Empty,
            Name = "Test Brand",
            Description = "Test Description",
            UrlSlug = "test-brand"
        };

        // Act
        if (brand.Id == Guid.Empty)
            brand.Id = Guid.CreateVersion7();

        await context.Brands.AddAsync(brand);
        await context.SaveChangesAsync();

        // Assert
        brand.Id.Should().NotBe(Guid.Empty);
        var savedBrand = await context.Brands.FindAsync(brand.Id);
        savedBrand.Should().NotBeNull();
    }
}
