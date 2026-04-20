using CrmSales.Api.Auditing;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Api.Master;

public class MasterDbContext(DbContextOptions<MasterDbContext> options) : DbContext(options)
{
    public const string SchemaName = "master";

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.Entity<Company>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(200);
            e.Property(c => c.Slug).IsRequired().HasMaxLength(63);
            e.HasIndex(c => c.Slug).IsUnique();
        });
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.TenantId).IsRequired().HasMaxLength(100);
            e.Property(a => a.EventType).IsRequired().HasMaxLength(100);
            e.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
            e.Property(a => a.EntityId).IsRequired().HasMaxLength(100);
            e.Property(a => a.Description).IsRequired().HasMaxLength(500);
            e.Property(a => a.Actor).IsRequired().HasMaxLength(200);
            e.HasIndex(a => new { a.TenantId, a.OccurredAt });
        });
    }
}
