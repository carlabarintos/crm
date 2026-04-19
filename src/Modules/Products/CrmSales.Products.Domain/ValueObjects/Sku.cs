using CrmSales.SharedKernel.Domain;

namespace CrmSales.Products.Domain.ValueObjects;

public sealed class Sku : ValueObject
{
    public string Value { get; }

    private Sku(string value) => Value = value;

    public static Sku Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SKU cannot be empty.", nameof(value));
        if (value.Length > 50)
            throw new ArgumentException("SKU cannot exceed 50 characters.", nameof(value));
        return new Sku(value.ToUpperInvariant());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
