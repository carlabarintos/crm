using CrmSales.SharedKernel.Catalog;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Quotes.Domain.Entities;

public sealed class QuoteLineItem : Entity<Guid>
{
    public Guid QuoteId { get; private set; }
    public Guid CatalogItemId { get; private set; }
    public string ItemName { get; private set; }
    public CatalogItemType ItemType { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal LineTotal => UnitPrice * Quantity;
    public decimal DiscountAmount => LineTotal * (DiscountPercent / 100);

    private QuoteLineItem() { ItemName = string.Empty; }

    internal static QuoteLineItem Create(Guid quoteId, Guid catalogItemId, string itemName,
        int quantity, decimal unitPrice, decimal discountPercent, CatalogItemType itemType = CatalogItemType.Product)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        if (unitPrice < 0) throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));
        if (discountPercent is < 0 or > 100)
            throw new ArgumentException("Discount must be between 0 and 100.", nameof(discountPercent));

        return new QuoteLineItem
        {
            Id = Guid.NewGuid(),
            QuoteId = quoteId,
            CatalogItemId = catalogItemId,
            ItemName = itemName.Trim(),
            ItemType = itemType,
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountPercent = discountPercent
        };
    }

    internal void UpdateQuantity(int quantity)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        Quantity = quantity;
    }

    internal void UpdateDetails(int quantity, decimal unitPrice, decimal discountPercent)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        if (unitPrice < 0) throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));
        if (discountPercent is < 0 or > 100)
            throw new ArgumentException("Discount must be between 0 and 100.", nameof(discountPercent));
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountPercent = discountPercent;
    }
}
