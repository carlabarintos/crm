using CrmSales.Orders.Domain.Entities;
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

        builder.Ignore(o => o.DomainEvents);
        builder.Ignore(o => o.TotalAmount);
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
        builder.Property(l => l.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4).IsRequired();
        builder.Ignore(l => l.DomainEvents);
        builder.Ignore(l => l.LineTotal);
        builder.ToTable("OrderLineItems");
    }
}
