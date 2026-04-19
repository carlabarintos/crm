using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Opportunities.Domain.Repositories;
using CrmSales.Opportunities.Infrastructure.Persistence;
using CrmSales.Opportunities.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrmSales.Opportunities.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOpportunitiesInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddScoped<TenantSchemaInterceptor>();
        services.AddDbContext<OpportunitiesDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(connectionString)
                .AddInterceptors(sp.GetRequiredService<TenantSchemaInterceptor>());
        });

        services.AddScoped<IOpportunityRepository, OpportunityRepository>();
        return services;
    }
}
