using CrmSales.Products.Domain.Repositories;
using CrmSales.Products.Infrastructure.Persistence;
using CrmSales.Products.Infrastructure.Repositories;
using CrmSales.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrmSales.Products.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProductsInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddScoped<TenantSchemaInterceptor>();
        services.AddDbContext<ProductsDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(connectionString)
                .AddInterceptors(sp.GetRequiredService<TenantSchemaInterceptor>());
        });

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductCategoryRepository, ProductCategoryRepository>();
        return services;
    }
}
