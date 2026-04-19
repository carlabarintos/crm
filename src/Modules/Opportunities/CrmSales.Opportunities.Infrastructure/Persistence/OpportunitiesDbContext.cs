using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Opportunities.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Opportunities.Infrastructure.Persistence;

public sealed class OpportunitiesDbContext(DbContextOptions<OpportunitiesDbContext> options)
    : DbContext(options)
{
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<Activity> Activities => Set<Activity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpportunitiesDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
