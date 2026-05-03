using CrmSales.Settings.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmSales.Settings.Infrastructure.Persistence.Configurations;

internal sealed class StorageSettingsConfiguration : IEntityTypeConfiguration<StorageSettings>
{
    public void Configure(EntityTypeBuilder<StorageSettings> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.MaxFileSizeBytes).IsRequired();
        builder.Property(s => s.MaxFilesPerOrder).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();
        builder.Ignore(s => s.DomainEvents);
        builder.ToTable("StorageSettings");
    }
}
