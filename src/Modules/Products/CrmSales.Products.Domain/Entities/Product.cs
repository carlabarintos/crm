using CrmSales.Products.Domain.Events;
using CrmSales.Products.Domain.ValueObjects;

namespace CrmSales.Products.Domain.Entities;

public sealed class Product : CatalogItem
{
    public Sku Sku { get; private set; }
    public int StockQuantity { get; private set; }
    public int ReorderPoint { get; private set; }

    private Product()
    {
        Sku = null!;
    }

    public static Product Create(
        string name,
        string? description,
        string sku,
        decimal price,
        string currency,
        Guid categoryId,
        int stockQuantity = 0,
        int reorderPoint = 10)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.", nameof(name));

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description?.Trim(),
            Sku = Sku.Create(sku),
            Price = Money.Of(price, currency),
            CategoryId = categoryId,
            StockQuantity = stockQuantity,
            ReorderPoint = reorderPoint,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        product.RaiseDomainEvent(new ProductCreatedEvent(
            product.Id, product.Name, product.Sku.Value,
            product.Price.Amount, product.Price.Currency));

        return product;
    }

    public void ChangePrice(decimal newAmount, string currency)
    {
        var oldPrice = Price;
        Price = Money.Of(newAmount, currency);
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new ProductPriceChangedEvent(
            Id, oldPrice.Amount, Price.Amount, Price.Currency));
    }

    public void AdjustStock(int quantity) => StockQuantity = Math.Max(0, StockQuantity + quantity);

    public bool IsLowStock() => StockQuantity <= ReorderPoint;

    public void SetReorderPoint(int reorderPoint)
    {
        ReorderPoint = Math.Max(0, reorderPoint);
        UpdatedAt = DateTime.UtcNow;
    }

    public override void Deactivate()
    {
        base.Deactivate();
        RaiseDomainEvent(new ProductDeactivatedEvent(Id));
    }
}
