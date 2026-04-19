using CrmSales.Products.Domain.Events;
using CrmSales.Products.Domain.ValueObjects;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Products.Domain.Entities;

public sealed class Product : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public Sku Sku { get; private set; }
    public Money Price { get; private set; }
    public Guid CategoryId { get; private set; }
    public bool IsActive { get; private set; }
    public int StockQuantity { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Product()
    {
        Name = string.Empty;
        Sku = null!;
        Price = null!;
    }

    public static Product Create(
        string name,
        string? description,
        string sku,
        decimal price,
        string currency,
        Guid categoryId,
        int stockQuantity = 0)
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
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        product.RaiseDomainEvent(new ProductCreatedEvent(
            product.Id, product.Name, product.Sku.Value,
            product.Price.Amount, product.Price.Currency));

        return product;
    }

    public void UpdateDetails(string name, string? description, Guid categoryId)
    {
        Name = name.Trim();
        Description = description?.Trim();
        CategoryId = categoryId;
        UpdatedAt = DateTime.UtcNow;
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

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new ProductDeactivatedEvent(Id));
    }
}
