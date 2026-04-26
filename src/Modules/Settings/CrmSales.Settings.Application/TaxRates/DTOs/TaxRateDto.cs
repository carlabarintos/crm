namespace CrmSales.Settings.Application.TaxRates.DTOs;

public record TaxRateDto(
    Guid Id,
    string Name,
    decimal Rate,
    string? Description,
    string? Region,
    bool IsDefault,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
