using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CrmSales.Products.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddProductsApplication(this IServiceCollection services)
    {
        // Wolverine discovers handlers via assembly scanning configured in the API host.
        // Register FluentValidation validators from this assembly.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
