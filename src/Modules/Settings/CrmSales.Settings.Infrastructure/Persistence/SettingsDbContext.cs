using CrmSales.Settings.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Settings.Infrastructure.Persistence;

public sealed class SettingsDbContext(DbContextOptions<SettingsDbContext> options)
    : DbContext(options)
{
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<EmailSettings> EmailSettings => Set<EmailSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SettingsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
