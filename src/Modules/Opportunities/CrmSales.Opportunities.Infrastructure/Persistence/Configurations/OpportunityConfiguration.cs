using CrmSales.Opportunities.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmSales.Opportunities.Infrastructure.Persistence.Configurations;

internal sealed class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Name).IsRequired().HasMaxLength(200);
        builder.Property(o => o.AccountName).IsRequired().HasMaxLength(200);
        builder.Property(o => o.ContactId);
        builder.Property(o => o.ContactName).IsRequired().HasMaxLength(200);
        builder.Property(o => o.ContactEmail).HasMaxLength(256);
        builder.Property(o => o.ContactPhone).HasMaxLength(50);
        builder.Property(o => o.Stage).IsRequired().HasConversion<string>();
        builder.Property(o => o.EstimatedValue).HasPrecision(18, 2).IsRequired();
        builder.Property(o => o.Currency).IsRequired().HasMaxLength(3);
        builder.Property(o => o.Probability).HasPrecision(5, 2).IsRequired();
        builder.Property(o => o.Description).HasMaxLength(4000);

        builder.HasMany(o => o.Activities)
               .WithOne()
               .HasForeignKey(a => a.OpportunityId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(o => o.DomainEvents);
        builder.Ignore(o => o.IsClosed);
        builder.Ignore(o => o.IsWon);

        builder.HasIndex(o => o.OwnerId);
        builder.HasIndex(o => o.Stage);
        builder.ToTable("Opportunities");
    }
}

internal sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Type).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Notes).IsRequired().HasMaxLength(4000);
        builder.Ignore(a => a.DomainEvents);
        builder.ToTable("Activities");
    }
}
