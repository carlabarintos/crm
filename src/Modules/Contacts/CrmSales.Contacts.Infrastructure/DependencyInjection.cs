using CrmSales.SharedKernel.MultiTenancy;
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
        services.AddScoped<TenantSchemaInterceptor>();
        services.AddDbContext<ContactsDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(connectionString)
                .AddInterceptors(sp.GetRequiredService<TenantSchemaInterceptor>());
        });

        services.AddScoped<IContactRepository, ContactRepository>();
        return services;
    }
}
