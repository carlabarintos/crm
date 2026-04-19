using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Users.Domain.Repositories;
using CrmSales.Users.Infrastructure.Persistence;
using CrmSales.Users.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrmSales.Users.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddScoped<TenantSchemaInterceptor>();
        services.AddDbContext<UsersDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(connectionString)
                .AddInterceptors(sp.GetRequiredService<TenantSchemaInterceptor>());
        });

        services.AddScoped<IUserRepository, UserRepository>();
        return services;
    }
}
