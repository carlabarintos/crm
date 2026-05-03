using CrmSales.Orders.Application.Services;
using CrmSales.Orders.Infrastructure.Storage;
using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Orders.Domain.Repositories;
using CrmSales.Orders.Infrastructure.Persistence;
using CrmSales.Orders.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrmSales.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersInfrastructure(
        this IServiceCollection services, string connectionString, IConfiguration configuration)
    {
        services.AddScoped<TenantSchemaInterceptor>();
        services.AddDbContext<OrdersDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(connectionString)
                .AddInterceptors(sp.GetRequiredService<TenantSchemaInterceptor>());
        });

        services.AddScoped<IOrderRepository, OrderRepository>();

        services.Configure<LocalStorageOptions>(configuration.GetSection("LocalStorage"));
        services.AddSingleton<IOrderDocumentStorage, LocalDiskOrderDocumentStorage>();

        services.Configure<ClamAvOptions>(configuration.GetSection("ClamAv"));
        services.AddScoped<IVirusScanService, ClamAvVirusScanService>();

        return services;
    }
}
