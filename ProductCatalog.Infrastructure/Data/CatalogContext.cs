using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProductCatalog.Api.Infrastructure.Domain;
using System.Reflection.Metadata;

namespace ProductCatalog.Api.Infrastructure.Data;
public class CatalogContext: DbContext
{
    public CatalogContext(DbContextOptions<CatalogContext> options, IConfiguration configuration) : base(options)
    {
    }

    public DbSet<Branch> Branches { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Dimension> Dimensions { get; set; }
    public DbSet<DimensionValue> DimensionValues { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Variant> Variants { get; set; }
    public DbSet<VariantDimentionValue> VariantDimentionValues { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Branch>()
            .Property(b => b.Name)
            .IsRequired();

        modelBuilder.Entity<Category>()
            .Property(c => c.Name)
            .IsRequired();
    }

}
