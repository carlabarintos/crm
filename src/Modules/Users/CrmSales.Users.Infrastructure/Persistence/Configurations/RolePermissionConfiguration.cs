using CrmSales.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmSales.Users.Infrastructure.Persistence.Configurations;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.RoleId).IsRequired();
        builder.Property(p => p.Permission).IsRequired().HasMaxLength(100);
        builder.HasIndex(p => p.RoleId);
        builder.ToTable("RolePermissions");
    }
}
