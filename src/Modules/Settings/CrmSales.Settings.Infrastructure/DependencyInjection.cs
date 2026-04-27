using CrmSales.Settings.Application.Services;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.Settings.Infrastructure.Persistence;
using CrmSales.Settings.Infrastructure.Repositories;
using CrmSales.Settings.Infrastructure.Services;
using CrmSales.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrmSales.Settings.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSettingsInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddScoped<TenantSchemaInterceptor>();
        services.AddDbContext<SettingsDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(connectionString)
                .AddInterceptors(sp.GetRequiredService<TenantSchemaInterceptor>());
        });

        services.AddScoped<ITaxRateRepository, TaxRateRepository>();
        services.AddScoped<IEmailTemplateRepository, EmailTemplateRepository>();
        services.AddScoped<IEmailSettingsRepository, EmailSettingsRepository>();
        services.AddSingleton<IEncryptionService, AesEncryptionService>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        return services;
    }
}
