using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace CrmSales.SharedKernel.MultiTenancy;

public sealed class TenantSchemaInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await SetSearchPathAsync(connection, cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SET search_path TO \"{tenantContext.TenantId}\"";
        cmd.ExecuteNonQuery();
    }

    private async Task SetSearchPathAsync(DbConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SET search_path TO \"{tenantContext.TenantId}\"";
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
