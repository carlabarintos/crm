using CrmSales.Orders.Domain.Entities;
using CrmSales.Orders.Domain.Repositories;
using CrmSales.SharedKernel.Messaging;
using CrmSales.SharedKernel.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace CrmSales.Orders.Infrastructure.Messaging;

/// <summary>
/// Wolverine handler for the QuoteAcceptedMessage integration event.
/// Wolverine delivers this message from RabbitMQ with automatic retry.
/// The method name "Handle" is the Wolverine convention.
/// </summary>
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

        // Idempotency guard — skip if an order already exists for this quote
        var existing = await orderRepository.GetByQuoteIdAsync(message.QuoteId, ct);
        if (existing is not null)
        {
            logger.LogWarning(
                "Order already exists for quote {QuoteId} — skipping duplicate message", message.QuoteId);
            return;
        }

        var items = message.LineItems.Select(l =>
            (l.ProductId, l.ProductName, l.Quantity, l.UnitPrice));

        var order = Order.CreateFromQuote(
            message.QuoteId,
            message.OpportunityId,
            message.OwnerId,
            message.Currency,
            items);

        if (message.AutoComplete)
            order.Complete();

        await orderRepository.AddAsync(order, ct);

        logger.LogInformation(
            "Order {OrderNumber} auto-created from accepted quote {QuoteNumber} (AutoComplete={AutoComplete})",
            order.OrderNumber, message.QuoteNumber, message.AutoComplete);
    }
}
