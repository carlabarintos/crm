using CrmSales.Quotes.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmSales.Quotes.Infrastructure.Persistence.Configurations;

internal sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();
        builder.Property(q => q.QuoteNumber).IsRequired().HasMaxLength(50);
        builder.Property(q => q.Currency).IsRequired().HasMaxLength(3);
        builder.Property(q => q.Status).IsRequired().HasConversion<string>();
        builder.Property(q => q.Notes).HasMaxLength(4000);

        builder.HasMany(q => q.LineItems)
               .WithOne()
               .HasForeignKey(l => l.QuoteId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(q => q.TaxRateName).HasMaxLength(100);
        builder.Property(q => q.TaxRatePercent).HasPrecision(5, 2);

        builder.Ignore(q => q.DomainEvents);
        builder.Ignore(q => q.SubTotal);
        builder.Ignore(q => q.DiscountTotal);
        builder.Ignore(q => q.TotalAmount);
        builder.Ignore(q => q.TaxAmount);
        builder.Ignore(q => q.GrandTotal);

        builder.HasIndex(q => q.QuoteNumber).IsUnique();
        builder.HasIndex(q => q.OpportunityId);
        builder.ToTable("Quotes");
    }
}

internal sealed class QuoteLineItemConfiguration : IEntityTypeConfiguration<QuoteLineItem>
{
    public void Configure(EntityTypeBuilder<QuoteLineItem> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4).IsRequired();
        builder.Property(l => l.DiscountPercent).HasPrecision(5, 2).IsRequired();

        builder.Ignore(l => l.DomainEvents);
        builder.Ignore(l => l.LineTotal);
        builder.Ignore(l => l.DiscountAmount);
        builder.ToTable("QuoteLineItems");
    }
}
