using CrmSales.Products.Domain.Events;
using CrmSales.Products.Domain.ValueObjects;

namespace CrmSales.Products.Domain.Entities;

public sealed class Service : CatalogItem
{
    public ServiceCode ServiceCode { get; private set; }
    public string? UnitOfMeasure { get; private set; }
    public int? EstimatedDurationMinutes { get; private set; }

    private Service()
    {
        ServiceCode = null!;
    }

    public static Service Create(
        string name,
        string? description,
        string serviceCode,
        decimal price,
        string currency,
        Guid categoryId,
        string? unitOfMeasure = null,
        int? estimatedDurationMinutes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Service name is required.", nameof(name));

        var service = new Service
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description?.Trim(),
            ServiceCode = ServiceCode.Create(serviceCode),
            Price = Money.Of(price, currency),
            CategoryId = categoryId,
            UnitOfMeasure = unitOfMeasure?.Trim(),
            EstimatedDurationMinutes = estimatedDurationMinutes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        service.RaiseDomainEvent(new ServiceCreatedEvent(
            service.Id, service.Name, service.ServiceCode.Value,
            service.Price.Amount, service.Price.Currency));

        return service;
    }

    public void ChangePrice(decimal newAmount, string currency)
    {
        Price = Money.Of(newAmount, currency);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateServiceDetails(string name, string? description, Guid categoryId,
        string? unitOfMeasure, int? estimatedDurationMinutes)
    {
        UpdateDetails(name, description, categoryId);
        UnitOfMeasure = unitOfMeasure?.Trim();
        EstimatedDurationMinutes = estimatedDurationMinutes;
    }
}
