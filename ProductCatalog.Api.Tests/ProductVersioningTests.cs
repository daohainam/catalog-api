using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Data;
using ProductCatalog.Infrastructure.Entity;
using System.Text.Json;

namespace ProductCatalog.Api.Tests;

public class ProductVersioningTests
{
    private ProductCatalogDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ProductCatalogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ProductCatalogDbContext(options);
    }

    private async Task<(Guid brandId, Guid categoryId)> SeedBrandAndCategory(ProductCatalogDbContext context)
    {
        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        await context.Brands.AddAsync(new Brand { Id = brandId, Name = "Test Brand", UrlSlug = "test-brand" });
        await context.Categories.AddAsync(new Category { Id = categoryId, Name = "Test Category", UrlSlug = "test-category" });
        await context.SaveChangesAsync();

        return (brandId, categoryId);
    }

    private Product CreateProduct(Guid brandId, Guid categoryId, string name = "Test Product")
    {
        return new Product
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            UrlSlug = name.ToLower().Replace(" ", "-"),
            Description = $"Description for {name}",
            BrandId = brandId,
            CategoryId = categoryId,
            IsActive = true,
            IsDeleted = false,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task NewProduct_ShouldHaveVersionOne()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (brandId, categoryId) = await SeedBrandAndCategory(context);
        var product = CreateProduct(brandId, categoryId);

        // Act
        await context.Products.AddAsync(product);
        await context.SaveChangesAsync();

        // Assert
        var savedProduct = await context.Products.FindAsync(product.Id);
        savedProduct.Should().NotBeNull();
        savedProduct!.Version.Should().Be(1);
    }

    [Fact]
    public async Task UpdateProduct_ShouldSaveHistoryAndIncrementVersion()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (brandId, categoryId) = await SeedBrandAndCategory(context);
        var product = CreateProduct(brandId, categoryId, "Original Name");

        await context.Products.AddAsync(product);
        await context.SaveChangesAsync();

        // Act - Simulate what UpdateProduct endpoint does
        var existingProduct = await context.Products.FindAsync(product.Id);

        var historyData = new
        {
            existingProduct!.Id,
            existingProduct.Name,
            existingProduct.UrlSlug,
            existingProduct.Description,
            existingProduct.BrandId,
            existingProduct.CategoryId,
            existingProduct.CreatedAt,
            existingProduct.UpdatedAt,
            existingProduct.IsActive,
            existingProduct.IsDeleted,
            existingProduct.Version
        };

        var history = new ProductHistory
        {
            Id = Guid.CreateVersion7(),
            ProductId = existingProduct.Id,
            Version = existingProduct.Version,
            ProductData = JsonSerializer.Serialize(historyData),
            CreatedAt = DateTime.UtcNow
        };

        await context.ProductHistories.AddAsync(history);

        existingProduct.Name = "Updated Name";
        existingProduct.Description = "Updated Description";
        existingProduct.UpdatedAt = DateTime.UtcNow;
        existingProduct.Version++;

        context.Products.Update(existingProduct);
        await context.SaveChangesAsync();

        // Assert
        var updatedProduct = await context.Products.FindAsync(product.Id);
        updatedProduct.Should().NotBeNull();
        updatedProduct!.Version.Should().Be(2);
        updatedProduct.Name.Should().Be("Updated Name");

        var savedHistory = await context.ProductHistories
            .Where(h => h.ProductId == product.Id)
            .SingleOrDefaultAsync();
        savedHistory.Should().NotBeNull();
        savedHistory!.Version.Should().Be(1);

        var historicalData = JsonSerializer.Deserialize<JsonElement>(savedHistory.ProductData);
        historicalData.GetProperty("Name").GetString().Should().Be("Original Name");
    }

    [Fact]
    public async Task MultipleUpdates_ShouldCreateMultipleHistoryRecords()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (brandId, categoryId) = await SeedBrandAndCategory(context);
        var product = CreateProduct(brandId, categoryId, "Version 1 Name");

        await context.Products.AddAsync(product);
        await context.SaveChangesAsync();

        // Act - First update
        var existingProduct = await context.Products.FindAsync(product.Id);
        await context.ProductHistories.AddAsync(new ProductHistory
        {
            Id = Guid.CreateVersion7(),
            ProductId = existingProduct!.Id,
            Version = existingProduct.Version,
            ProductData = JsonSerializer.Serialize(new { existingProduct.Name, existingProduct.Version }),
            CreatedAt = DateTime.UtcNow
        });
        existingProduct.Name = "Version 2 Name";
        existingProduct.Version++;
        await context.SaveChangesAsync();

        // Act - Second update
        await context.ProductHistories.AddAsync(new ProductHistory
        {
            Id = Guid.CreateVersion7(),
            ProductId = existingProduct.Id,
            Version = existingProduct.Version,
            ProductData = JsonSerializer.Serialize(new { existingProduct.Name, existingProduct.Version }),
            CreatedAt = DateTime.UtcNow
        });
        existingProduct.Name = "Version 3 Name";
        existingProduct.Version++;
        await context.SaveChangesAsync();

        // Assert
        var finalProduct = await context.Products.FindAsync(product.Id);
        finalProduct!.Version.Should().Be(3);
        finalProduct.Name.Should().Be("Version 3 Name");

        var histories = await context.ProductHistories
            .Where(h => h.ProductId == product.Id)
            .OrderBy(h => h.Version)
            .ToListAsync();

        histories.Should().HaveCount(2);
        histories[0].Version.Should().Be(1);
        histories[1].Version.Should().Be(2);
    }

    [Fact]
    public async Task ProductHistory_ShouldStoreCompleteProductDataAsJson()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (brandId, categoryId) = await SeedBrandAndCategory(context);
        var product = CreateProduct(brandId, categoryId, "Complete Data Product");

        await context.Products.AddAsync(product);
        await context.SaveChangesAsync();

        // Act
        var existingProduct = await context.Products.FindAsync(product.Id);

        var historyData = new
        {
            existingProduct!.Id,
            existingProduct.Name,
            existingProduct.UrlSlug,
            existingProduct.Description,
            existingProduct.BrandId,
            existingProduct.CategoryId,
            existingProduct.CreatedAt,
            existingProduct.UpdatedAt,
            existingProduct.IsActive,
            existingProduct.IsDeleted,
            existingProduct.Version
        };

        var history = new ProductHistory
        {
            Id = Guid.CreateVersion7(),
            ProductId = existingProduct.Id,
            Version = existingProduct.Version,
            ProductData = JsonSerializer.Serialize(historyData),
            CreatedAt = DateTime.UtcNow
        };

        await context.ProductHistories.AddAsync(history);
        await context.SaveChangesAsync();

        // Assert
        var savedHistory = await context.ProductHistories.FindAsync(history.Id);
        savedHistory.Should().NotBeNull();

        var deserializedData = JsonSerializer.Deserialize<JsonElement>(savedHistory!.ProductData);
        deserializedData.GetProperty("Id").GetGuid().Should().Be(product.Id);
        deserializedData.GetProperty("Name").GetString().Should().Be("Complete Data Product");
        deserializedData.GetProperty("UrlSlug").GetString().Should().Be("complete-data-product");
        deserializedData.GetProperty("Description").GetString().Should().Be("Description for Complete Data Product");
        deserializedData.GetProperty("BrandId").GetGuid().Should().Be(brandId);
        deserializedData.GetProperty("CategoryId").GetGuid().Should().Be(categoryId);
        deserializedData.GetProperty("IsActive").GetBoolean().Should().BeTrue();
        deserializedData.GetProperty("IsDeleted").GetBoolean().Should().BeFalse();
        deserializedData.GetProperty("Version").GetInt64().Should().Be(1);
    }

    [Fact]
    public async Task RevertProduct_ShouldRestoreHistoricalData()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (brandId, categoryId) = await SeedBrandAndCategory(context);
        var product = CreateProduct(brandId, categoryId, "Original Name");

        await context.Products.AddAsync(product);
        await context.SaveChangesAsync();

        // Save version 1 to history and update
        var existingProduct = await context.Products.FindAsync(product.Id);

        var historyData = new
        {
            existingProduct!.Id,
            existingProduct.Name,
            existingProduct.UrlSlug,
            existingProduct.Description,
            existingProduct.BrandId,
            existingProduct.CategoryId,
            existingProduct.CreatedAt,
            existingProduct.UpdatedAt,
            existingProduct.IsActive,
            existingProduct.IsDeleted,
            existingProduct.Version
        };

        await context.ProductHistories.AddAsync(new ProductHistory
        {
            Id = Guid.CreateVersion7(),
            ProductId = existingProduct.Id,
            Version = existingProduct.Version,
            ProductData = JsonSerializer.Serialize(historyData),
            CreatedAt = DateTime.UtcNow
        });

        existingProduct.Name = "Updated Name";
        existingProduct.Description = "Updated Description";
        existingProduct.Version++;
        await context.SaveChangesAsync();

        // Act - Revert to version 1
        var history = await context.ProductHistories
            .Where(h => h.ProductId == product.Id && h.Version == 1)
            .SingleOrDefaultAsync();

        history.Should().NotBeNull();

        var historicalProduct = JsonSerializer.Deserialize<JsonElement>(history!.ProductData);

        // Save current state before reverting
        var currentHistoryData = new
        {
            existingProduct.Id,
            existingProduct.Name,
            existingProduct.UrlSlug,
            existingProduct.Description,
            existingProduct.BrandId,
            existingProduct.CategoryId,
            existingProduct.CreatedAt,
            existingProduct.UpdatedAt,
            existingProduct.IsActive,
            existingProduct.IsDeleted,
            existingProduct.Version
        };

        await context.ProductHistories.AddAsync(new ProductHistory
        {
            Id = Guid.CreateVersion7(),
            ProductId = existingProduct.Id,
            Version = existingProduct.Version,
            ProductData = JsonSerializer.Serialize(currentHistoryData),
            CreatedAt = DateTime.UtcNow
        });

        existingProduct.Name = historicalProduct.GetProperty("Name").GetString()!;
        existingProduct.Description = historicalProduct.GetProperty("Description").GetString()!;
        existingProduct.IsActive = historicalProduct.GetProperty("IsActive").GetBoolean();
        existingProduct.IsDeleted = historicalProduct.GetProperty("IsDeleted").GetBoolean();
        existingProduct.Version++;
        await context.SaveChangesAsync();

        // Assert
        var revertedProduct = await context.Products.FindAsync(product.Id);
        revertedProduct.Should().NotBeNull();
        revertedProduct!.Name.Should().Be("Original Name");
        revertedProduct.Description.Should().Be("Description for Original Name");
        revertedProduct.Version.Should().Be(3); // Version incremented after revert

        var allHistories = await context.ProductHistories
            .Where(h => h.ProductId == product.Id)
            .OrderBy(h => h.Version)
            .ToListAsync();
        allHistories.Should().HaveCount(2); // version 1 and version 2 in history
    }

    [Fact]
    public async Task GetProductHistory_ShouldReturnHistoriesOrderedByVersionDescending()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var (brandId, categoryId) = await SeedBrandAndCategory(context);
        var productId = Guid.CreateVersion7();

        // Create 5 history records
        for (int i = 1; i <= 5; i++)
        {
            await context.ProductHistories.AddAsync(new ProductHistory
            {
                Id = Guid.CreateVersion7(),
                ProductId = productId,
                Version = i,
                ProductData = JsonSerializer.Serialize(new { Version = i, Name = $"Version {i}" }),
                CreatedAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();

        // Act
        var histories = await context.ProductHistories
            .AsNoTracking()
            .Where(h => h.ProductId == productId)
            .OrderByDescending(h => h.Version)
            .ToListAsync();

        // Assert
        histories.Should().HaveCount(5);
        histories[0].Version.Should().Be(5);
        histories[1].Version.Should().Be(4);
        histories[2].Version.Should().Be(3);
        histories[3].Version.Should().Be(2);
        histories[4].Version.Should().Be(1);
    }
}
