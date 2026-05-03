using CrmSales.Orders.Domain.Entities;
using CrmSales.SharedKernel.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmSales.Orders.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
        builder.Property(o => o.Currency).IsRequired().HasMaxLength(3);
        builder.Property(o => o.Status).IsRequired().HasConversion<string>();
        builder.Property(o => o.ShippingAddress).HasMaxLength(1000);
        builder.Property(o => o.Notes).HasMaxLength(4000);

        builder.HasMany(o => o.LineItems)
               .WithOne()
               .HasForeignKey(l => l.OrderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Documents)
               .WithOne()
               .HasForeignKey(d => d.OrderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(o => o.TaxRateName).HasMaxLength(100);
        builder.Property(o => o.TaxRatePercent).HasPrecision(5, 2);
        builder.Property(o => o.QuoteDiscountPercent).HasPrecision(5, 2).IsRequired();

        builder.Ignore(o => o.DomainEvents);
        builder.Ignore(o => o.SubTotal);
        builder.Ignore(o => o.DiscountTotal);
        builder.Ignore(o => o.TotalAmount);
        builder.Ignore(o => o.QuoteDiscountAmount);
        builder.Ignore(o => o.TaxableAmount);
        builder.Ignore(o => o.TaxAmount);
        builder.Ignore(o => o.GrandTotal);
        builder.Ignore(o => o.CanBeCancelled);

        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => o.QuoteId).IsUnique();
        builder.HasIndex(o => o.Status);
        builder.ToTable("Orders");
    }
}

internal sealed class OrderLineItemConfiguration : IEntityTypeConfiguration<OrderLineItem>
{
    public void Configure(EntityTypeBuilder<OrderLineItem> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.ItemName).IsRequired().HasMaxLength(200);
        builder.Property(l => l.ItemType).IsRequired().HasConversion<string>().HasDefaultValue(CatalogItemType.Product);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.DiscountPercent).HasPrecision(5, 2).IsRequired();
        builder.Ignore(l => l.DomainEvents);
        builder.Ignore(l => l.LineTotal);
        builder.Ignore(l => l.DiscountAmount);
        builder.ToTable("OrderLineItems");
    }
}
