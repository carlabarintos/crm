using CrmSales.Settings.Domain.Entities;
using CrmSales.Settings.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmSales.Settings.Infrastructure.Persistence.Configurations;

internal sealed class EmailSettingsConfiguration : IEntityTypeConfiguration<EmailSettings>
{
    public void Configure(EntityTypeBuilder<EmailSettings> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Host).IsRequired().HasMaxLength(500);
        builder.Property(s => s.Port).IsRequired();
        builder.Property(s => s.Username).IsRequired().HasMaxLength(500);
        builder.Property(s => s.Password).IsRequired().HasColumnType("text");
        builder.Property(s => s.FromName).IsRequired().HasMaxLength(500);
        builder.Property(s => s.FromAddress).IsRequired().HasMaxLength(500);
        builder.Property(s => s.EnableSsl).IsRequired();
        builder.Property(s => s.IsEnabled).IsRequired();
        builder.Property(s => s.AuthMode)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>()
            .HasDefaultValue(SmtpAuthMode.UsernamePassword);
        builder.Property(s => s.UpdatedAt).IsRequired();
        builder.Ignore(s => s.DomainEvents);
        builder.ToTable("EmailSettings");
    }
}
