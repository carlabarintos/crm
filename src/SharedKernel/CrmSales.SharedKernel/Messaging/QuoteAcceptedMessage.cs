namespace CrmSales.SharedKernel.Messaging;

public record QuoteAcceptedMessage(
    Guid QuoteId,
    string QuoteNumber,
    Guid OpportunityId,
    decimal TotalAmount,
    string Currency,
    Guid OwnerId,
    IReadOnlyList<QuoteLineItemMessage> LineItems,
    string TenantId = "master",
    bool AutoComplete = false,
    string? TaxRateName = null,
    decimal TaxRatePercent = 0,
    Guid? ContactId = null,
    decimal QuoteDiscountPercent = 0);

public record QuoteLineItemMessage(
    Guid CatalogItemId,
    string ItemName,
    int Quantity,
    decimal UnitPrice,
    string ItemType = "Product",
    decimal DiscountPercent = 0);
