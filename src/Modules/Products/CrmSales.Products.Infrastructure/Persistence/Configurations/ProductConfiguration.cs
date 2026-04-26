using CrmSales.Products.Domain.Entities;
using CrmSales.Products.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmSales.Products.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.StockQuantity).IsRequired();
        builder.Property(p => p.ReorderPoint).IsRequired().HasDefaultValue(10);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        builder.OwnsOne(p => p.Sku, sku =>
        {
            sku.Property(s => s.Value)
               .HasColumnName("Sku")
               .IsRequired()
               .HasMaxLength(50);
            sku.HasIndex(s => s.Value).IsUnique();
        });

        builder.OwnsOne(p => p.Price, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Price").HasPrecision(18, 4).IsRequired();
            money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3).IsRequired();
        });
        builder.HasIndex(p => p.CategoryId);

        builder.Ignore(p => p.DomainEvents);
        builder.ToTable("Products");
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
