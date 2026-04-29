using CrmSales.Products.Domain.Entities;
using CrmSales.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Products.Infrastructure.Persistence;

public sealed class ProductsDbContext(DbContextOptions<ProductsDbContext> options)
    : DbContext(options)
{
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ProductCategory> Categories => Set<ProductCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
