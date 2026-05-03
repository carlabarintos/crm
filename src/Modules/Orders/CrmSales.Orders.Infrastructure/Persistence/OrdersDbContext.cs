using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Orders.Infrastructure.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLineItem> LineItems => Set<OrderLineItem>();
    public DbSet<OrderDocument> OrderDocuments => Set<OrderDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
