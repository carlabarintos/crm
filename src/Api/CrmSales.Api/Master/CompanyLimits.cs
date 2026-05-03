namespace CrmSales.Api.Master;

public class CompanyLimits
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public int? MaxProducts { get; set; }
    public int? MaxCategories { get; set; }
    public int? MaxServices { get; set; }
    public int? MaxContacts { get; set; }
    public int? MaxOpportunities { get; set; }
    public int? MaxQuotes { get; set; }
    public int? MaxOrders { get; set; }
    public int? MaxUsers { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
}
