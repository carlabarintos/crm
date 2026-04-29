using CrmSales.Orders.Domain.Entities;
using CrmSales.Orders.Domain.Repositories;
using CrmSales.SharedKernel.Catalog;
using CrmSales.SharedKernel.Messaging;
using CrmSales.SharedKernel.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace CrmSales.Orders.Infrastructure.Messaging;

public static class QuoteAcceptedHandler
{
    public static async Task Handle(
        QuoteAcceptedMessage message,
        IOrderRepository orderRepository,
        ITenantContext tenantContext,
        ILogger logger,
        CancellationToken ct)
    {
        tenantContext.TenantId = message.TenantId;

        var existing = await orderRepository.GetByQuoteIdAsync(message.QuoteId, ct);
        if (existing is not null)
        {
            logger.LogWarning(
                "Order already exists for quote {QuoteId} — skipping duplicate message", message.QuoteId);
            return;
        }

        var items = message.LineItems.Select(l =>
        {
            var itemType = Enum.TryParse<CatalogItemType>(l.ItemType, out var t) ? t : CatalogItemType.Product;
            return (l.CatalogItemId, l.ItemName, l.Quantity, l.UnitPrice, itemType, l.DiscountPercent);
        });

        var customerId = message.ContactId ?? Guid.Empty;
        var order = Order.CreateFromQuote(
            message.QuoteId,
            message.OpportunityId,
            customerId,
            message.Currency,
            items,
            taxRateName: message.TaxRateName,
            taxRatePercent: message.TaxRatePercent,
            quoteDiscountPercent: message.QuoteDiscountPercent);

        if (message.AutoComplete)
            order.Complete();

        await orderRepository.AddAsync(order, ct);

        logger.LogInformation(
            "Order {OrderNumber} auto-created from accepted quote {QuoteNumber} (AutoComplete={AutoComplete})",
            order.OrderNumber, message.QuoteNumber, message.AutoComplete);
    }
}
