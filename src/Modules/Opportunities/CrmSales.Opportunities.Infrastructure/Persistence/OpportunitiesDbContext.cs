using CrmSales.Opportunities.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Opportunities.Infrastructure.Persistence;

public sealed class OpportunitiesDbContext(DbContextOptions<OpportunitiesDbContext> options) : DbContext(options)
{
    public const string SchemaName = "opportunities";

    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<Activity> Activities => Set<Activity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpportunitiesDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
