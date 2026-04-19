using CrmSales.Orders.Domain.Repositories;
using CrmSales.Orders.Infrastructure.Persistence;
using CrmSales.Orders.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrmSales.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<OrdersDbContext>(opts =>
            opts.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", OrdersDbContext.SchemaName)));

        services.AddScoped<IOrderRepository, OrderRepository>();
        return services;
    }
}
