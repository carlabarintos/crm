using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Quotes.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Quotes.Infrastructure.Persistence;

public sealed class QuotesDbContext(DbContextOptions<QuotesDbContext> options)
    : DbContext(options)
{
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLineItem> LineItems => Set<QuoteLineItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuotesDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
