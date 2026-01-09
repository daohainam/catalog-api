using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Infrastructure.Data;
using ProductCatalog.Infrastructure.Entity;

namespace ProductCatalog.Api.Tests;

public class ProductServiceTests
{
    private ProductCatalogDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ProductCatalogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ProductCatalogDbContext(options);
    }

    [Fact]
    public async Task CreateProduct_ShouldAddProductToDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var brand = new Brand
        {
            Id = brandId,
            Name = "Test Brand",
            UrlSlug = "test-brand"
        };

        var category = new Category
        {
            Id = categoryId,
            Name = "Test Category",
            UrlSlug = "test-category"
        };

        await context.Brands.AddAsync(brand);
        await context.Categories.AddAsync(category);
        await context.SaveChangesAsync();

        var product = new Product
        {
            Id = Guid.CreateVersion7(),
            Name = "Test Product",
            UrlSlug = "test-product",
            Description = "Test Description",
            BrandId = brandId,
            CategoryId = categoryId,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await context.Products.AddAsync(product);
        await context.SaveChangesAsync();

        // Assert
        var savedProduct = await context.Products.FindAsync(product.Id);
        savedProduct.Should().NotBeNull();
        savedProduct!.Name.Should().Be("Test Product");
        savedProduct.UrlSlug.Should().Be("test-product");
        savedProduct.Description.Should().Be("Test Description");
        savedProduct.BrandId.Should().Be(brandId);
        savedProduct.CategoryId.Should().Be(categoryId);
        savedProduct.IsActive.Should().BeTrue();
        savedProduct.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateProduct_WithVariants_ShouldCreateProductAndVariants()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.CreateVersion7();

        var brand = new Brand { Id = brandId, Name = "Test Brand", UrlSlug = "test-brand" };
        var category = new Category { Id = categoryId, Name = "Test Category", UrlSlug = "test-category" };

        await context.Brands.AddAsync(brand);
        await context.Categories.AddAsync(category);
        await context.SaveChangesAsync();

        var product = new Product
        {
            Id = productId,
            Name = "Test Product",
            UrlSlug = "test-product",
            Description = "Test Description",
            BrandId = brandId,
            CategoryId = categoryId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var variant1 = new Variant
        {
            Id = Guid.CreateVersion7(),
            ProductId = productId,
            Sku = "SKU-001",
            BarCode = "1234567890",
            Price = 99.99m,
            Description = "Variant 1",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var variant2 = new Variant
        {
            Id = Guid.CreateVersion7(),
            ProductId = productId,
            Sku = "SKU-002",
            BarCode = "0987654321",
            Price = 149.99m,
            Description = "Variant 2",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await context.Products.AddAsync(product);
        await context.Variants.AddAsync(variant1);
        await context.Variants.AddAsync(variant2);
        await context.SaveChangesAsync();

        // Assert
        var savedProduct = await context.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == productId);

        savedProduct.Should().NotBeNull();
        savedProduct!.Variants.Should().HaveCount(2);
        savedProduct.Variants.Should().Contain(v => v.Sku == "SKU-001");
        savedProduct.Variants.Should().Contain(v => v.Sku == "SKU-002");
    }

    [Fact]
    public async Task UpdateProduct_ShouldModifyExistingProduct()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.CreateVersion7();

        var brand = new Brand { Id = brandId, Name = "Test Brand", UrlSlug = "test-brand" };
        var category = new Category { Id = categoryId, Name = "Test Category", UrlSlug = "test-category" };

        await context.Brands.AddAsync(brand);
        await context.Categories.AddAsync(category);
        await context.SaveChangesAsync();

        var product = new Product
        {
            Id = productId,
            Name = "Original Product",
            UrlSlug = "original-product",
            Description = "Original Description",
            BrandId = brandId,
            CategoryId = categoryId,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await context.Products.AddAsync(product);
        await context.SaveChangesAsync();

        // Act
        var existingProduct = await context.Products.FindAsync(productId);
        existingProduct!.Name = "Updated Product";
        existingProduct.Description = "Updated Description";
        existingProduct.IsActive = true;
        existingProduct.UpdatedAt = DateTime.UtcNow;

        context.Products.Update(existingProduct);
        await context.SaveChangesAsync();

        // Assert
        var updatedProduct = await context.Products.FindAsync(productId);
        updatedProduct.Should().NotBeNull();
        updatedProduct!.Name.Should().Be("Updated Product");
        updatedProduct.Description.Should().Be("Updated Description");
        updatedProduct.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetProduct_WithVariantsAndDimensions_ShouldLoadAllRelatedData()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.CreateVersion7();
        var variantId = Guid.CreateVersion7();

        var brand = new Brand { Id = brandId, Name = "Test Brand", UrlSlug = "test-brand" };
        var category = new Category { Id = categoryId, Name = "Test Category", UrlSlug = "test-category" };
        var dimension = new Dimension
        {
            Id = "color",
            Name = "Color",
            DisplayType = "dropdown",
            DefaultValue = ""
        };

        await context.Brands.AddAsync(brand);
        await context.Categories.AddAsync(category);
        await context.Dimensions.AddAsync(dimension);
        await context.SaveChangesAsync();

        var product = new Product
        {
            Id = productId,
            Name = "Test Product",
            UrlSlug = "test-product",
            Description = "Test Description",
            BrandId = brandId,
            CategoryId = categoryId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var productDimension = new ProductDimension
        {
            ProductId = productId,
            DimensionId = "color"
        };

        var variant = new Variant
        {
            Id = variantId,
            ProductId = productId,
            Sku = "SKU-001",
            BarCode = "1234567890",
            Price = 99.99m,
            Description = "Variant",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var variantDimensionValue = new VariantDimensionValue
        {
            VariantId = variantId,
            DimensionId = "color",
            Value = "red"
        };

        await context.Products.AddAsync(product);
        await context.ProductDimensions.AddAsync(productDimension);
        await context.Variants.AddAsync(variant);
        await context.VariantDimensionValues.AddAsync(variantDimensionValue);
        await context.SaveChangesAsync();

        // Act
        var loadedProduct = await context.Products
            .Include(p => p.Variants)
            .ThenInclude(v => v.DimensionValues)
            .FirstOrDefaultAsync(p => p.Id == productId);

        // Assert
        loadedProduct.Should().NotBeNull();
        loadedProduct!.Variants.Should().ContainSingle();
        loadedProduct.Variants[0].DimensionValues.Should().ContainSingle();
        loadedProduct.Variants[0].DimensionValues[0].DimensionId.Should().Be("color");
        loadedProduct.Variants[0].DimensionValues[0].Value.Should().Be("red");
    }

    [Fact]
    public async Task GetProducts_ShouldReturnPaginatedResults()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var brand = new Brand { Id = brandId, Name = "Test Brand", UrlSlug = "test-brand" };
        var category = new Category { Id = categoryId, Name = "Test Category", UrlSlug = "test-category" };

        await context.Brands.AddAsync(brand);
        await context.Categories.AddAsync(category);
        await context.SaveChangesAsync();

        for (int i = 0; i < 25; i++)
        {
            await context.Products.AddAsync(new Product
            {
                Id = Guid.CreateVersion7(),
                Name = $"Product {i}",
                UrlSlug = $"product-{i}",
                Description = $"Description {i}",
                BrandId = brandId,
                CategoryId = categoryId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();

        // Act
        var firstPage = await context.Products
            .Skip(0)
            .Take(10)
            .ToListAsync();

        var secondPage = await context.Products
            .Skip(10)
            .Take(10)
            .ToListAsync();

        var thirdPage = await context.Products
            .Skip(20)
            .Take(10)
            .ToListAsync();

        // Assert
        firstPage.Should().HaveCount(10);
        secondPage.Should().HaveCount(10);
        thirdPage.Should().HaveCount(5);
    }
}
