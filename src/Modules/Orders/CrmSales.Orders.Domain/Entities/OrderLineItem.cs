using CrmSales.SharedKernel.Catalog;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Orders.Domain.Entities;

public sealed class OrderLineItem : Entity<Guid>
{
    public Guid OrderId { get; private set; }
    public Guid CatalogItemId { get; private set; }
    public string ItemName { get; private set; }
    public CatalogItemType ItemType { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal LineTotal => Quantity * UnitPrice;
    public decimal DiscountAmount => LineTotal * (DiscountPercent / 100);

    private OrderLineItem() { ItemName = string.Empty; }

    internal static OrderLineItem Create(Guid orderId, Guid catalogItemId, string itemName,
        int quantity, decimal unitPrice, CatalogItemType itemType = CatalogItemType.Product,
        decimal discountPercent = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CatalogItemId = catalogItemId,
            ItemName = itemName.Trim(),
            ItemType = itemType,
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountPercent = discountPercent
        };

    internal void Update(int quantity, decimal unitPrice)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
