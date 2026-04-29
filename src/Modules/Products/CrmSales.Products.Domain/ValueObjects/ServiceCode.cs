using CrmSales.SharedKernel.Domain;

namespace CrmSales.Products.Domain.ValueObjects;

public sealed class ServiceCode : ValueObject
{
    public string Value { get; }

    private ServiceCode(string value) => Value = value;

    public static ServiceCode Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Service code cannot be empty.", nameof(value));
        if (value.Length > 50)
            throw new ArgumentException("Service code cannot exceed 50 characters.", nameof(value));
        return new ServiceCode(value.ToUpperInvariant());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
