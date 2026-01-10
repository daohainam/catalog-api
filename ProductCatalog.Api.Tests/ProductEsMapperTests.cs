using FluentAssertions;
using ProductCatalog.Events;
using ProductCatalog.Search;

namespace ProductCatalog.Api.Tests;

public class ProductEsMapperTests
{
    [Fact]
    public void Map_ShouldMapProductCreatedEventToIndexDocument()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var productCreatedEvent = new ProductCreatedEvent
        {
            ProductId = productId,
            Product = new ProductInfo
            {
                Name = "Test Product",
                UrlSlug = "test-product",
                Description = "Test Description",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Brand = new BrandInfo
                {
                    BrandId = brandId,
                    Name = "Test Brand",
                    Description = "Brand Description",
                    LogoUrl = "https://example.com/logo.png"
                },
                Path = new List<CategoryInfo>
                {
                    new CategoryInfo
                    {
                        CategoryId = categoryId,
                        Name = "Test Category",
                        Description = "Category Description",
                        UrlSlug = "test-category"
                    }
                },
                Groups = new List<GroupInfo>
                {
                    new GroupInfo
                    {
                        GroupId = groupId,
                        Name = "Test Group"
                    }
                },
                Dimensions = new List<DimensionInfo>
                {
                    new DimensionInfo
                    {
                        DimensionId = "color",
                        Name = "Color",
                        DisplayType = "dropdown",
                        Values = new List<DimensionValueInfo>
                        {
                            new DimensionValueInfo { Value = "red", DisplayValue = "Red" },
                            new DimensionValueInfo { Value = "blue", DisplayValue = "Blue" }
                        }
                    }
                },
                Variants = new List<VariantInfo>
                {
                    new VariantInfo
                    {
                        Id = variantId,
                        ProductId = productId,
                        Sku = "TEST-SKU-001",
                        BarCode = "1234567890",
                        Price = 99.99m,
                        Description = "Variant Description",
                        IsActive = true,
                        InStock = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        DimensionValues = new List<VariantDimensionValueInfo>
                        {
                            new VariantDimensionValueInfo { DimensionId = "color", Value = "red" }
                        }
                    }
                },
                Images = new List<ProductImageInfo>
                {
                    new ProductImageInfo
                    {
                        ImageId = Guid.NewGuid(),
                        ProductId = productId,
                        ImageUrl = "https://example.com/image.jpg",
                        AltText = "Test Image",
                        SortOrder = 1
                    }
                }
            }
        };

        // Act
        var result = ProductEsMapper.Map(productCreatedEvent);

        // Assert
        result.Should().NotBeNull();
        result.ProductId.Should().Be(productId);
        result.Name.Should().Be("Test Product");
        result.Slug.Should().Be("test-product");
        result.Description.Should().Be("Test Description");
        result.IsActive.Should().BeTrue();
        
        result.BrandId.Should().Be(brandId);
        result.BrandName.Should().Be("Test Brand");
        
        result.CategoryId.Should().Be(categoryId);
        result.CategoryName.Should().Be("Test Category");
        result.CategorySlug.Should().Be("test-category");
        result.CategoryPath.Should().Be("test-category");
        
        result.GroupIds.Should().ContainSingle().Which.Should().Be(groupId);
        result.GroupNames.Should().ContainSingle().Which.Should().Be("Test Group");
        
        result.Dimensions.Should().ContainSingle();
        result.Dimensions[0].DimensionId.Should().Be("color");
        result.Dimensions[0].Name.Should().Be("Color");
        result.Dimensions[0].DisplayType.Should().Be("dropdown");
        
        result.Variants.Should().ContainSingle();
        var variant = result.Variants[0];
        variant.VariantId.Should().Be(variantId);
        variant.Sku.Should().Be("TEST-SKU-001");
        variant.BarCode.Should().Be("1234567890");
        variant.Price.Should().Be(99.99m);
        variant.InStock.Should().BeTrue();
        variant.IsActive.Should().BeTrue();
        
        variant.Dimensions.Should().ContainSingle();
        variant.Dimensions[0].DimensionId.Should().Be("color");
        variant.Dimensions[0].Value.Should().Be("red");
        variant.Dimensions[0].DisplayValue.Should().Be("Red");
        
        result.PriceMin.Should().Be(99.99m);
        result.InStock.Should().BeTrue();
        result.VariantCount.Should().Be(1);
        
        result.PrimaryVariant.Should().NotBeNull();
        result.PrimaryVariant!.VariantId.Should().Be(variantId);
        result.PrimaryVariant.Price.Should().Be(99.99m);
        result.PrimaryVariant.InStock.Should().BeTrue();
        
        result.Images.Should().ContainSingle();
        result.Images[0].Url.Should().Be("https://example.com/image.jpg");
        result.Images[0].Alt.Should().Be("Test Image");
        result.Images[0].SortOrder.Should().Be(1);
        
        result.Suggest.Should().NotBeNull();
        result.Suggest!.Input.Should().Contain("Test Product");
        result.Suggest.Input.Should().Contain("Test Brand");
    }

    [Fact]
    public void Map_ShouldChooseLowestPriceInStockVariantAsPrimary()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var variant1Id = Guid.NewGuid();
        var variant2Id = Guid.NewGuid();
        var variant3Id = Guid.NewGuid();

        var productCreatedEvent = new ProductCreatedEvent
        {
            ProductId = productId,
            Product = new ProductInfo
            {
                Name = "Test Product",
                UrlSlug = "test-product",
                Description = "Test",
                IsActive = true,
                Brand = new BrandInfo { BrandId = Guid.NewGuid(), Name = "Brand" },
                Path = new List<CategoryInfo>(),
                Dimensions = new List<DimensionInfo>(),
                Variants = new List<VariantInfo>
                {
                    new VariantInfo
                    {
                        Id = variant1Id,
                        ProductId = productId,
                        Sku = "SKU-1",
                        Price = 150m,
                        InStock = true,
                        IsActive = true
                    },
                    new VariantInfo
                    {
                        Id = variant2Id,
                        ProductId = productId,
                        Sku = "SKU-2",
                        Price = 100m,
                        InStock = true,
                        IsActive = true
                    },
                    new VariantInfo
                    {
                        Id = variant3Id,
                        ProductId = productId,
                        Sku = "SKU-3",
                        Price = 50m,
                        InStock = false,
                        IsActive = true
                    }
                }
            }
        };

        // Act
        var result = ProductEsMapper.Map(productCreatedEvent);

        // Assert
        result.PrimaryVariant.Should().NotBeNull();
        result.PrimaryVariant!.VariantId.Should().Be(variant2Id);
        result.PrimaryVariant.Price.Should().Be(100m);
        result.PrimaryVariant.InStock.Should().BeTrue();
        result.PriceMin.Should().Be(50m);
    }

    [Fact]
    public void Map_ShouldHandleEmptyVariants()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productCreatedEvent = new ProductCreatedEvent
        {
            ProductId = productId,
            Product = new ProductInfo
            {
                Name = "Test Product",
                UrlSlug = "test-product",
                Description = "Test",
                IsActive = true,
                Brand = new BrandInfo { BrandId = Guid.NewGuid(), Name = "Brand" },
                Path = new List<CategoryInfo>(),
                Dimensions = new List<DimensionInfo>(),
                Variants = new List<VariantInfo>()
            }
        };

        // Act
        var result = ProductEsMapper.Map(productCreatedEvent);

        // Assert
        result.Variants.Should().BeEmpty();
        result.PrimaryVariant.Should().BeNull();
        result.PriceMin.Should().BeNull();
        result.InStock.Should().BeFalse();
        result.VariantCount.Should().Be(0);
    }

    [Fact]
    public void Map_ShouldBuildCategoryPathFromMultipleLevels()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var category1Id = Guid.NewGuid();
        var category2Id = Guid.NewGuid();
        var category3Id = Guid.NewGuid();

        var productCreatedEvent = new ProductCreatedEvent
        {
            ProductId = productId,
            Product = new ProductInfo
            {
                Name = "Test Product",
                UrlSlug = "test-product",
                Description = "Test",
                IsActive = true,
                Brand = new BrandInfo { BrandId = Guid.NewGuid(), Name = "Brand" },
                Path = new List<CategoryInfo>
                {
                    new CategoryInfo { CategoryId = category1Id, Name = "Electronics", UrlSlug = "electronics" },
                    new CategoryInfo { CategoryId = category2Id, Name = "Computers", UrlSlug = "computers" },
                    new CategoryInfo { CategoryId = category3Id, Name = "Laptops", UrlSlug = "laptops" }
                },
                Dimensions = new List<DimensionInfo>(),
                Variants = new List<VariantInfo>()
            }
        };

        // Act
        var result = ProductEsMapper.Map(productCreatedEvent);

        // Assert
        result.CategoryId.Should().Be(category3Id);
        result.CategoryName.Should().Be("Laptops");
        result.CategorySlug.Should().Be("laptops");
        result.CategoryPath.Should().Be("electronics/computers/laptops");
    }
}
