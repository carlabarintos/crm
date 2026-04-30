using CrmSales.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrmSales.Users.Infrastructure.Persistence.Configurations;

internal sealed class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.HasKey(ur => ur.Id);
        builder.Property(ur => ur.Id).ValueGeneratedNever();
        builder.Property(ur => ur.UserId).IsRequired();
        builder.Property(ur => ur.RoleId).IsRequired();
        builder.Property(ur => ur.AssignedAt).IsRequired();
        builder.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();
        builder.ToTable("UserRoles");
    }
}
