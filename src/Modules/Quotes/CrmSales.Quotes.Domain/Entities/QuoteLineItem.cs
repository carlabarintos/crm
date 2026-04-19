using CrmSales.SharedKernel.Domain;

namespace CrmSales.Quotes.Domain.Entities;

public sealed class QuoteLineItem : Entity<Guid>
{
    public Guid QuoteId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal LineTotal => UnitPrice * Quantity;
    public decimal DiscountAmount => LineTotal * (DiscountPercent / 100);

    private QuoteLineItem() { ProductName = string.Empty; }

    internal static QuoteLineItem Create(Guid quoteId, Guid productId, string productName,
        int quantity, decimal unitPrice, decimal discountPercent)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        if (unitPrice < 0) throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));
        if (discountPercent is < 0 or > 100)
            throw new ArgumentException("Discount must be between 0 and 100.", nameof(discountPercent));

        return new QuoteLineItem
        {
            Id = Guid.NewGuid(),
            QuoteId = quoteId,
            ProductId = productId,
            ProductName = productName.Trim(),
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
}
