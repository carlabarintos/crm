using CrmSales.Orders.Domain.Events;
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

    public decimal TotalAmount => _lineItems.Sum(l => l.LineTotal);
    public bool CanBeCancelled => Status is OrderStatus.Pending or OrderStatus.Confirmed;

    private Order() { OrderNumber = string.Empty; Currency = string.Empty; }

    public static Order CreateFromQuote(
        Guid quoteId, Guid opportunityId, Guid customerId,
        string currency, IEnumerable<(Guid ProductId, string ProductName, int Qty, decimal UnitPrice)> items,
        string? shippingAddress = null, string? notes = null)
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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var (productId, productName, qty, unitPrice) in items)
            order._lineItems.Add(OrderLineItem.Create(order.Id, productId, productName, qty, unitPrice));

        order.RaiseDomainEvent(new OrderCreatedEvent(order.Id, order.OrderNumber, quoteId, order.TotalAmount, currency));
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

    public void AddLineItem(Guid productId, string productName, int quantity, decimal unitPrice)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Line items can only be added to pending orders.");
        _lineItems.Add(OrderLineItem.Create(Id, productId, productName, quantity, unitPrice));
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

    public void Complete()
    {
        if (Status == OrderStatus.Pending) Confirm();
        if (Status == OrderStatus.Confirmed) StartProcessing();
        if (Status == OrderStatus.Processing) Ship();
        if (Status == OrderStatus.Shipped) Deliver();
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
