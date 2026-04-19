using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Quotes.Domain.Repositories;
using CrmSales.Quotes.Infrastructure.Persistence;
using CrmSales.Quotes.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrmSales.Quotes.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddQuotesInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddScoped<TenantSchemaInterceptor>();
        services.AddDbContext<QuotesDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(connectionString)
                .AddInterceptors(sp.GetRequiredService<TenantSchemaInterceptor>());
        });

        services.AddScoped<IQuoteRepository, QuoteRepository>();
        return services;
    }
}
