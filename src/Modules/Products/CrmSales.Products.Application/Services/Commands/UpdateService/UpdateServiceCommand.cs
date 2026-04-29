using CrmSales.SharedKernel.Application;

namespace CrmSales.Products.Application.Services.Commands.UpdateService;

public record UpdateServiceCommand(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    Guid CategoryId,
    string? UnitOfMeasure,
    int? EstimatedDurationMinutes) : ICommand;
