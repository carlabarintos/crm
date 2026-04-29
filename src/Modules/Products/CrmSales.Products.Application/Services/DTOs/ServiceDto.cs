namespace CrmSales.Products.Application.Services.DTOs;

public record ServiceDto(
    Guid Id,
    string Name,
    string? Description,
    string ServiceCode,
    decimal Price,
    string Currency,
    Guid CategoryId,
    string? CategoryName,
    bool IsActive,
    string? UnitOfMeasure,
    int? EstimatedDurationMinutes,
    DateTime CreatedAt,
    DateTime UpdatedAt);
