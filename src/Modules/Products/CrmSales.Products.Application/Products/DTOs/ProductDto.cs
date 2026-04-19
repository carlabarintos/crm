namespace CrmSales.Products.Application.Products.DTOs;

public record ProductDto(
    Guid Id,
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    string Currency,
    Guid CategoryId,
    string? CategoryName,
    bool IsActive,
    int StockQuantity,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record ProductCategoryDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive);
