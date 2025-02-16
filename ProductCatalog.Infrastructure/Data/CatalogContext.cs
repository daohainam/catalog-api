using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProductCatalog.Infrastructure.Entities;
using System.Reflection.Metadata;

namespace ProductCatalog.Infrastructure.Data;
public class CatalogContext : DbContext
{
    public DbSet<Brand> Brands { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Dimension> Dimensions { get; set; }
    public DbSet<DimensionValue> DimensionValues { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Variant> Variants { get; set; }
    public DbSet<VariantDimentionValue> VariantDimentionValues { get; set; }

    public CatalogContext()
    {
    }

    public CatalogContext(DbContextOptions<CatalogContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Brand>()
            .Property(b => b.Name)
            .IsRequired();

        modelBuilder.Entity<Category>()
            .Property(c => c.Name)
            .IsRequired();
    }

}
