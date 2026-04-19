namespace CrmSales.SharedKernel.MultiTenancy;

public interface ITenantContext
{
    string TenantId { get; set; }
}
