namespace CrmSales.SharedKernel.Authorization;

public static class Permissions
{
    public const string ManageProducts       = "products.manage";
    public const string ViewProducts         = "products.view";
    public const string ManageContacts       = "contacts.manage";
    public const string ViewContacts         = "contacts.view";
    public const string ManageOpportunities  = "opportunities.manage";
    public const string ViewOpportunities    = "opportunities.view";
    public const string ViewOwnOpportunities = "opportunities.view_own";
    public const string ManageQuotes         = "quotes.manage";
    public const string SendQuotes           = "quotes.send";
    public const string ApproveQuotes        = "quotes.approve";
    public const string ManageOrders         = "orders.manage";
    public const string ViewOrders           = "orders.view";
    public const string ManageUsers          = "users.manage";
    public const string ManageRoles          = "roles.manage";
    public const string ManageSettings       = "settings.manage";
    public const string ViewAudit            = "audit.view";
    public const string ViewOwnAudit         = "audit.view_own";

    public static IReadOnlyList<string> All =>
    [
        ManageProducts, ViewProducts,
        ManageContacts, ViewContacts,
        ManageOpportunities, ViewOpportunities, ViewOwnOpportunities,
        ManageQuotes, SendQuotes, ApproveQuotes,
        ManageOrders, ViewOrders,
        ManageUsers, ManageRoles,
        ManageSettings,
        ViewAudit, ViewOwnAudit
    ];
}
