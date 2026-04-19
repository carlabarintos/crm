using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CrmSales.Opportunities.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOpportunitiesApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
