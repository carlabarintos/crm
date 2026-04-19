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
        services.AddDbContext<UsersDbContext>(opts =>
            opts.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", UsersDbContext.SchemaName)));

        services.AddScoped<IUserRepository, UserRepository>();
        return services;
    }
}
