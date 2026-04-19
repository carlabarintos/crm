using CrmSales.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Orders.Infrastructure.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public const string SchemaName = "orders";

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLineItem> LineItems => Set<OrderLineItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
