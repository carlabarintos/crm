using CrmSales.SharedKernel.MultiTenancy;
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
        services.AddScoped<TenantSchemaInterceptor>();
        services.AddDbContext<OrdersDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(connectionString)
                .AddInterceptors(sp.GetRequiredService<TenantSchemaInterceptor>());
        });

        services.AddScoped<IOrderRepository, OrderRepository>();
        return services;
    }
}
