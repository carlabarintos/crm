using CrmSales.Products.Domain.Entities;
using CrmSales.Products.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmSales.Products.Infrastructure.Persistence.Configurations;

internal sealed class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.HasDiscriminator<string>("Discriminator")
            .HasValue<Product>("Product")
            .HasValue<Service>("Service");

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.IsActive).IsRequired();
        builder.Property(c => c.CategoryId).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.OwnsOne(c => c.Price, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Price").HasPrecision(18, 4).IsRequired();
            money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3).IsRequired();
        });

        builder.HasIndex(c => c.CategoryId);
        builder.Ignore(c => c.DomainEvents);
        builder.ToTable("CatalogItems");
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.OwnsOne(p => p.Sku, sku =>
        {
            sku.Property(s => s.Value)
               .HasColumnName("Sku")
               .HasMaxLength(50)
               .IsRequired(false);
            sku.HasIndex(s => s.Value).IsUnique().HasFilter("\"Sku\" IS NOT NULL");
        });

        builder.Property(p => p.StockQuantity).HasDefaultValue(0);
        builder.Property(p => p.ReorderPoint).HasDefaultValue(10);
    }
}

internal sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.OwnsOne(s => s.ServiceCode, sc =>
        {
            sc.Property(c => c.Value)
              .HasColumnName("ServiceCode")
              .HasMaxLength(50);
            sc.HasIndex(c => c.Value).IsUnique().HasFilter("\"ServiceCode\" IS NOT NULL");
        });

        builder.Property(s => s.UnitOfMeasure).HasMaxLength(50);
        builder.Property(s => s.EstimatedDurationMinutes);
    }
}

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.ToTable("Categories");
    }
}
