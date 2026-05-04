using CrmSales.Orders.Domain.Events;
using CrmSales.SharedKernel.Catalog;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Orders.Domain.Entities;

public enum OrderStatus { Pending, Confirmed, Processing, Shipped, Delivered, Cancelled }

public sealed class Order : AggregateRoot<Guid>
{
    public string OrderNumber { get; private set; }
    public Guid QuoteId { get; private set; }
    public Guid OpportunityId { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public string Currency { get; private set; }
    public string? ShippingAddress { get; private set; }
    public string? Notes { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<OrderLineItem> _lineItems = [];
    public IReadOnlyCollection<OrderLineItem> LineItems => _lineItems.AsReadOnly();

    private readonly List<OrderDocument> _documents = [];
    public IReadOnlyCollection<OrderDocument> Documents => _documents.AsReadOnly();

    public string? TaxRateName { get; private set; }
    public decimal TaxRatePercent { get; private set; }
    public decimal QuoteDiscountPercent { get; private set; }

    public decimal SubTotal => _lineItems.Sum(l => l.LineTotal);
    public decimal DiscountTotal => _lineItems.Sum(l => l.DiscountAmount);
    public decimal TotalAmount => SubTotal - DiscountTotal;
    public decimal QuoteDiscountAmount => Math.Round(TotalAmount * (QuoteDiscountPercent / 100m), 4);
    public decimal TaxableAmount => TotalAmount - QuoteDiscountAmount;
    public decimal TaxAmount => Math.Round(TaxableAmount * (TaxRatePercent / 100m), 4);
    public decimal GrandTotal => TaxableAmount + TaxAmount;
    public bool CanBeCancelled => Status is OrderStatus.Pending or OrderStatus.Confirmed;

    private Order() { OrderNumber = string.Empty; Currency = string.Empty; }

    public static Order CreateFromQuote(
        Guid quoteId, Guid opportunityId, Guid customerId,
        string currency,
        IEnumerable<(Guid? CatalogItemId, string ItemName, int Qty, decimal UnitPrice, CatalogItemType ItemType, decimal DiscountPercent)> items,
        string? shippingAddress = null, string? notes = null,
        string? taxRateName = null, decimal taxRatePercent = 0,
        decimal quoteDiscountPercent = 0)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = GenerateOrderNumber(),
            QuoteId = quoteId,
            OpportunityId = opportunityId,
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            Currency = currency,
            ShippingAddress = shippingAddress?.Trim(),
            Notes = notes?.Trim(),
            TaxRateName = taxRateName,
            TaxRatePercent = taxRatePercent,
            QuoteDiscountPercent = quoteDiscountPercent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var (catalogItemId, itemName, qty, unitPrice, itemType, discountPercent) in items)
            order._lineItems.Add(OrderLineItem.Create(order.Id, catalogItemId, itemName, qty, unitPrice, itemType, discountPercent));

        order.RaiseDomainEvent(new OrderCreatedEvent(order.Id, order.OrderNumber, quoteId, order.GrandTotal, currency));
        return order;
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Only pending orders can be confirmed.");
        Status = OrderStatus.Confirmed;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new OrderConfirmedEvent(Id, OrderNumber, CustomerId, TotalAmount, Currency));
    }

    public void StartProcessing()
    {
        if (Status != OrderStatus.Confirmed)
            throw new InvalidOperationException("Only confirmed orders can be processed.");
        Status = OrderStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Ship(string? trackingInfo = null)
    {
        if (Status != OrderStatus.Processing)
            throw new InvalidOperationException("Only processing orders can be shipped.");
        Status = OrderStatus.Shipped;
        ShippedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new OrderShippedEvent(Id, OrderNumber, trackingInfo));
    }

    public void Deliver()
    {
        if (Status != OrderStatus.Shipped)
            throw new InvalidOperationException("Only shipped orders can be delivered.");
        Status = OrderStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddLineItem(Guid? catalogItemId, string itemName, int quantity, decimal unitPrice,
        CatalogItemType itemType = CatalogItemType.Product)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Line items can only be added to pending orders.");
        if (catalogItemId == null && string.IsNullOrWhiteSpace(itemName))
            throw new ArgumentException("Item name is required for custom line items.", nameof(itemName));
        _lineItems.Add(OrderLineItem.Create(Id, catalogItemId, itemName, quantity, unitPrice, itemType));
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateLineItem(Guid lineItemId, int quantity, decimal unitPrice)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Line items can only be updated on pending orders.");
        var item = _lineItems.FirstOrDefault(l => l.Id == lineItemId)
            ?? throw new InvalidOperationException("Line item not found.");
        item.Update(quantity, unitPrice);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveLineItem(Guid lineItemId)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Line items can only be removed from pending orders.");
        var item = _lineItems.FirstOrDefault(l => l.Id == lineItemId)
            ?? throw new InvalidOperationException("Line item not found.");
        _lineItems.Remove(item);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyTax(string taxRateName, decimal taxRatePercent)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Tax can only be changed on pending orders.");
        if (string.IsNullOrWhiteSpace(taxRateName))
            throw new ArgumentException("Tax rate name is required.", nameof(taxRateName));
        if (taxRatePercent < 0 || taxRatePercent > 100)
            throw new ArgumentException("Tax rate must be between 0 and 100.", nameof(taxRatePercent));
        TaxRateName = taxRateName.Trim();
        TaxRatePercent = taxRatePercent;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveTax()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Tax can only be changed on pending orders.");
        TaxRateName = null;
        TaxRatePercent = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetQuoteDiscount(decimal percent)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Discount can only be changed on pending orders.");
        if (percent < 0 || percent > 100)
            throw new ArgumentException("Discount must be between 0 and 100.", nameof(percent));
        QuoteDiscountPercent = percent;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveQuoteDiscount()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Discount can only be changed on pending orders.");
        QuoteDiscountPercent = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        if (Status == OrderStatus.Pending) Confirm();
        if (Status == OrderStatus.Confirmed) StartProcessing();
        if (Status == OrderStatus.Processing) Ship();
        if (Status == OrderStatus.Shipped) Deliver();
    }

    public void AttachDocument(OrderDocument doc) => _documents.Add(doc);

    public OrderDocument? RemoveDocument(Guid docId)
    {
        var doc = _documents.FirstOrDefault(d => d.Id == docId);
        if (doc is not null) _documents.Remove(doc);
        return doc;
    }

    public void Cancel(string reason)
    {
        if (!CanBeCancelled)
            throw new InvalidOperationException("This order cannot be cancelled.");
        Status = OrderStatus.Cancelled;
        Notes = reason;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new OrderCancelledEvent(Id, OrderNumber, reason));
    }

    private static string GenerateOrderNumber() =>
        $"ORD-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
}
