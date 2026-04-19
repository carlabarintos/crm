using CrmSales.Contacts.Infrastructure.Persistence;
using CrmSales.Opportunities.Infrastructure.Persistence;
using CrmSales.Orders.Infrastructure.Persistence;
using CrmSales.Products.Infrastructure.Persistence;
using CrmSales.Quotes.Infrastructure.Persistence;
using CrmSales.Users.Infrastructure.Persistence;
using CrmSales.SharedKernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace CrmSales.Api.MultiTenancy;

public class TenantProvisioner(IServiceProvider serviceProvider, ILogger<TenantProvisioner> logger)
{
    public async Task ProvisionAsync(string tenantId)
    {
        using var scope = serviceProvider.CreateScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenant.TenantId = tenantId;

        var conn = scope.ServiceProvider.GetRequiredService<ProductsDbContext>().Database.GetDbConnection();
        await conn.OpenAsync();
        bool schemaHasTables;
        try
        {
            await using var schemaCmd = conn.CreateCommand();
            schemaCmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{tenantId}\"";
            await schemaCmd.ExecuteNonQueryAsync();

            await using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = $"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = '{tenantId}')";
            schemaHasTables = (bool)(await checkCmd.ExecuteScalarAsync())!;
        }
        finally { await conn.CloseAsync(); }

        if (schemaHasTables)
        {
            logger.LogInformation("Tenant schema '{TenantId}' already has tables — skipping provisioning.", tenantId);
            return;
        }

        logger.LogInformation("Provisioning tables for tenant '{TenantId}'.", tenantId);
        await CreateTablesAsync(scope.ServiceProvider.GetRequiredService<ProductsDbContext>());
        await CreateTablesAsync(scope.ServiceProvider.GetRequiredService<UsersDbContext>());
        await CreateTablesAsync(scope.ServiceProvider.GetRequiredService<ContactsDbContext>());
        await CreateTablesAsync(scope.ServiceProvider.GetRequiredService<OpportunitiesDbContext>());
        await CreateTablesAsync(scope.ServiceProvider.GetRequiredService<QuotesDbContext>());
        await CreateTablesAsync(scope.ServiceProvider.GetRequiredService<OrdersDbContext>());
    }

    private static Task CreateTablesAsync(DbContext ctx)
        => ctx.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
}
