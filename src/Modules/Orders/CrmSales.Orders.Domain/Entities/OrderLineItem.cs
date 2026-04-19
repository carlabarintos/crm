using CrmSales.SharedKernel.Domain;

namespace CrmSales.Orders.Domain.Entities;

public sealed class OrderLineItem : Entity<Guid>
{
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal => Quantity * UnitPrice;

    private OrderLineItem() { ProductName = string.Empty; }

    internal static OrderLineItem Create(Guid orderId, Guid productId, string productName, int quantity, decimal unitPrice) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            ProductName = productName.Trim(),
            Quantity = quantity,
            UnitPrice = unitPrice
        };
}
