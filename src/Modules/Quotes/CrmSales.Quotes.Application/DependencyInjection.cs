using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CrmSales.Quotes.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddQuotesApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
