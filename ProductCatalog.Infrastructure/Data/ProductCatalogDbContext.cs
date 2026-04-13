using ProductCatalog.Infrastructure.Entity;

namespace ProductCatalog.Infrastructure.Data;
public class ProductCatalogDbContext(DbContextOptions<ProductCatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products { get; internal set; } = default!;
    public DbSet<ProductDimension> ProductDimensions { get; internal set; } = default!;
    public DbSet<Dimension> Dimensions { get; internal set; }
    public DbSet<DimensionValue> DimensionValues { get; internal set; }
    public DbSet<Category> Categories { get; internal set; }
    public DbSet<Group> Groups { get; internal set; }
    public DbSet<Variant> Variants { get; internal set; }
    public DbSet<VariantDimensionValue> VariantDimensionValues { get; internal set; }
    public DbSet<ProductImage> ProductImages { get; internal set; }
    public DbSet<Image> Images { get; internal set; }
    public DbSet<Brand> Brands { get; internal set; }
    public DbSet<GroupProduct> GroupProducts { get; internal set; }
    public DbSet<ProductHistory> ProductHistories { get; internal set; } = default!;
    public DbSet<LogTailingOutboxMessage> LogTailingOutboxMessages { get; internal set; } = default!;


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .HasMany(e => e.Groups)
            .WithMany(e => e.Products)
            .UsingEntity<GroupProduct>();

        // Add indices for common query patterns and foreign keys
        modelBuilder.Entity<Product>()
            .HasIndex(p => p.BrandId)
            .HasDatabaseName("IX_Products_BrandId");

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.CategoryId)
            .HasDatabaseName("IX_Products_CategoryId");

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.IsActive)
            .HasDatabaseName("IX_Products_IsActive");

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.UrlSlug)
            .HasDatabaseName("IX_Products_UrlSlug");

        modelBuilder.Entity<Variant>()
            .HasIndex(v => v.ProductId)
            .HasDatabaseName("IX_Variants_ProductId");

        modelBuilder.Entity<Variant>()
            .HasIndex(v => v.Sku)
            .HasDatabaseName("IX_Variants_Sku");

        modelBuilder.Entity<Variant>()
            .HasIndex(v => v.IsActive)
            .HasDatabaseName("IX_Variants_IsActive");

        modelBuilder.Entity<ProductDimension>()
            .HasIndex(pd => pd.ProductId)
            .HasDatabaseName("IX_ProductDimensions_ProductId");

        modelBuilder.Entity<ProductDimension>()
            .HasIndex(pd => pd.DimensionId)
            .HasDatabaseName("IX_ProductDimensions_DimensionId");

        modelBuilder.Entity<VariantDimensionValue>()
            .HasIndex(vdv => vdv.VariantId)
            .HasDatabaseName("IX_VariantDimensionValues_VariantId");

        modelBuilder.Entity<DimensionValue>()
            .HasIndex(dv => dv.DimensionId)
            .HasDatabaseName("IX_DimensionValues_DimensionId");

        modelBuilder.Entity<Brand>()
            .HasIndex(b => b.UrlSlug)
            .HasDatabaseName("IX_Brands_UrlSlug");

        modelBuilder.Entity<Category>()
            .HasIndex(c => c.UrlSlug)
            .HasDatabaseName("IX_Categories_UrlSlug");

        modelBuilder.Entity<ProductHistory>()
            .HasIndex(ph => ph.ProductId)
            .HasDatabaseName("IX_ProductHistories_ProductId");

        modelBuilder.Entity<ProductHistory>()
            .HasIndex(ph => new { ph.ProductId, ph.Version })
            .IsUnique()
            .HasDatabaseName("IX_ProductHistories_ProductId_Version");
    }
}
