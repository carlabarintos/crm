using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CrmSales.Contacts.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddContactsApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
