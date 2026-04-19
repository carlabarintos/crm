using CrmSales.Contacts.Domain.Repositories;
using CrmSales.Contacts.Infrastructure.Persistence;
using CrmSales.Contacts.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrmSales.Contacts.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddContactsInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<ContactsDbContext>(opts =>
            opts.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ContactsDbContext.SchemaName)));

        services.AddScoped<IContactRepository, ContactRepository>();
        return services;
    }
}
