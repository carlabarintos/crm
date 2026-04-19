using Microsoft.EntityFrameworkCore;

namespace CrmSales.Api.Master;

public class MasterDbContext(DbContextOptions<MasterDbContext> options) : DbContext(options)
{
    public const string SchemaName = "master";

    public DbSet<Company> Companies => Set<Company>();

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
    }
}
