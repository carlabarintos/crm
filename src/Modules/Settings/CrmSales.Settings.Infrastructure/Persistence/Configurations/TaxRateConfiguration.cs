using CrmSales.Settings.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmSales.Settings.Infrastructure.Persistence.Configurations;

internal sealed class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Rate).HasPrecision(5, 2).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.Region).HasMaxLength(100);
        builder.Property(t => t.IsDefault).IsRequired();
        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();

        builder.HasIndex(t => t.Name).IsUnique();
        builder.Ignore(t => t.DomainEvents);

        builder.ToTable("TaxRates");
    }
}
