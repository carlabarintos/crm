using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CrmSales.Settings.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSettingsApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
