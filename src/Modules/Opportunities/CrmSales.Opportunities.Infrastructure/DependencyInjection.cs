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
        services.AddDbContext<OpportunitiesDbContext>(opts =>
            opts.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", OpportunitiesDbContext.SchemaName)));

        services.AddScoped<IOpportunityRepository, OpportunityRepository>();
        return services;
    }
}
