namespace CrmSales.Api.Master;

public class SubscriptionPlan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public decimal PricePhp { get; set; }
    public int BillingCycleMonths { get; set; }
    public string Description { get; set; } = "";

    // Usage limits (null = unlimited / not restricted)
    public int? MaxUsers { get; set; }
    public int? MaxContacts { get; set; }
    public int? MaxProducts { get; set; }
    public int? MaxServices { get; set; }
    public int? MaxCategories { get; set; }
    public int? MaxOpportunities { get; set; }
    public int? MaxQuotes { get; set; }
    public int? MaxOrders { get; set; }

    // Extra bullet points beyond what limits describe
    public string? AdditionalFeatures { get; set; }

    public bool IsFree { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public static SubscriptionPlan Create(string name, decimal pricePhp, int billingCycleMonths,
        string description, bool isFree) => new()
    {
        Id = Guid.NewGuid(),
        Name = name.Trim(),
        PricePhp = isFree ? 0 : pricePhp,
        BillingCycleMonths = billingCycleMonths,
        Description = description.Trim(),
        IsFree = isFree,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    // Generates the feature bullet list: limits first, then additional features.
    // null limit = not mentioned (unlimited); set limit = "Up to N things".
    public string[] GetFeatureList()
    {
        var items = new List<string>();

        AddLimit(items, MaxUsers, "user", "users");
        AddLimit(items, MaxContacts, "contact", "contacts");
        AddLimit(items, MaxProducts, "product", "products");
        AddLimit(items, MaxServices, "service", "services");
        AddLimit(items, MaxCategories, "category", "categories");
        AddLimit(items, MaxOpportunities, "opportunity", "opportunities");
        AddLimit(items, MaxQuotes, "quote", "quotes");
        AddLimit(items, MaxOrders, "order", "orders");

        if (!string.IsNullOrWhiteSpace(AdditionalFeatures))
            items.AddRange(AdditionalFeatures
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return [.. items];
    }

    private static void AddLimit(List<string> items, int? limit, string singular, string plural)
    {
        if (!limit.HasValue) return;
        items.Add(limit == 0
            ? $"No {plural}"
            : $"Up to {limit:N0} {(limit == 1 ? singular : plural)}");
    }
}
