using CrmSales.SharedKernel.Domain;

namespace CrmSales.Settings.Domain.Entities;

public sealed class TaxRate : AggregateRoot<Guid>
{
    public string Name { get; private set; } = default!;
    public decimal Rate { get; private set; }
    public string? Description { get; private set; }
    public string? Region { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private TaxRate() { }

    public static TaxRate Create(string name, decimal rate, string? description, string? region)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tax rate name is required.", nameof(name));
        if (rate < 0 || rate > 100)
            throw new ArgumentException("Tax rate must be between 0 and 100.", nameof(rate));

        return new TaxRate
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Rate = rate,
            Description = description?.Trim(),
            Region = region?.Trim(),
            IsDefault = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, decimal rate, string? description, string? region)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tax rate name is required.", nameof(name));
        if (rate < 0 || rate > 100)
            throw new ArgumentException("Tax rate must be between 0 and 100.", nameof(rate));

        Name = name.Trim();
        Rate = rate;
        Description = description?.Trim();
        Region = region?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAsDefault() { IsDefault = true; UpdatedAt = DateTime.UtcNow; }
    public void ClearDefault() { IsDefault = false; UpdatedAt = DateTime.UtcNow; }
    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
    public void Activate() { IsActive = true; UpdatedAt = DateTime.UtcNow; }
}
