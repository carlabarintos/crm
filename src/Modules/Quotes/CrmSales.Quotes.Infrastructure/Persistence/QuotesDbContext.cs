using CrmSales.Quotes.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Quotes.Infrastructure.Persistence;

public sealed class QuotesDbContext(DbContextOptions<QuotesDbContext> options) : DbContext(options)
{
    public const string SchemaName = "quotes";

    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLineItem> LineItems => Set<QuoteLineItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuotesDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
