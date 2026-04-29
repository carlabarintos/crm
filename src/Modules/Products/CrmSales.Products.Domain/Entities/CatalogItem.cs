using CrmSales.Products.Domain.ValueObjects;
using CrmSales.SharedKernel.Domain;

namespace CrmSales.Products.Domain.Entities;

public abstract class CatalogItem : AggregateRoot<Guid>
{
    public string Name { get; protected set; }
    public string? Description { get; protected set; }
    public Money Price { get; protected set; }
    public Guid CategoryId { get; protected set; }
    public bool IsActive { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime UpdatedAt { get; protected set; }

    protected CatalogItem()
    {
        Name = string.Empty;
        Price = null!;
    }

    public void UpdateDetails(string name, string? description, Guid categoryId)
    {
        Name = name.Trim();
        Description = description?.Trim();
        CategoryId = categoryId;
        UpdatedAt = DateTime.UtcNow;
    }

    public virtual void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public virtual void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
