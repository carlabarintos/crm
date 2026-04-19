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
        services.AddDbContext<QuotesDbContext>(opts =>
            opts.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", QuotesDbContext.SchemaName)));

        services.AddScoped<IQuoteRepository, QuoteRepository>();
        return services;
    }
}
