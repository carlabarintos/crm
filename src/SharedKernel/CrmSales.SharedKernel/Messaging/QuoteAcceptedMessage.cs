namespace CrmSales.SharedKernel.Messaging;

/// <summary>
/// Integration message published to RabbitMQ when a Quote is accepted.
/// Consumed by the Orders module to auto-create an Order.
/// Lives in SharedKernel so both Quotes (publisher) and Orders (consumer) share the same contract.
/// </summary>
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
    Guid? ContactId = null);

public record QuoteLineItemMessage(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);
