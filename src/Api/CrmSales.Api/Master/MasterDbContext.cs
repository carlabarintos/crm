using CrmSales.Api.Auditing;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Api.Master;

public class MasterDbContext(DbContextOptions<MasterDbContext> options) : DbContext(options)
{
    public const string SchemaName = "master";

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();

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
        modelBuilder.Entity<AccessRequest>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Name).IsRequired().HasMaxLength(200);
            e.Property(a => a.Company).IsRequired().HasMaxLength(200);
            e.Property(a => a.Email).IsRequired().HasMaxLength(254);
            e.Property(a => a.Phone).HasMaxLength(50);
            e.Property(a => a.Message).HasMaxLength(2000);
            e.Property(a => a.Status).IsRequired().HasMaxLength(20);
            e.HasIndex(a => a.Status);
            e.HasIndex(a => a.RequestedAt);
        });
    }
}
