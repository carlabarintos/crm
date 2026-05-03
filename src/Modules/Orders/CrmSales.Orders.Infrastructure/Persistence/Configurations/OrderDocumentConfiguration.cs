using CrmSales.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmSales.Orders.Infrastructure.Persistence.Configurations;

internal sealed class OrderDocumentConfiguration : IEntityTypeConfiguration<OrderDocument>
{
    public void Configure(EntityTypeBuilder<OrderDocument> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.FileName).IsRequired().HasMaxLength(500);
        builder.Property(d => d.StorageKey).IsRequired().HasMaxLength(1000);
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Type).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(d => d.FileSizeBytes).IsRequired();
        builder.Property(d => d.Notes).HasMaxLength(1000);
        builder.Property(d => d.UploadedAt).IsRequired();
        builder.HasIndex(d => d.OrderId);
        builder.Ignore(d => d.DomainEvents);
        builder.ToTable("OrderDocuments");
    }
}
