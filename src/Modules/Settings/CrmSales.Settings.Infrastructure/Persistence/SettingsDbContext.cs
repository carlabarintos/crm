using CrmSales.Settings.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Settings.Infrastructure.Persistence;

public sealed class SettingsDbContext(DbContextOptions<SettingsDbContext> options)
    : DbContext(options)
{
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SettingsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
