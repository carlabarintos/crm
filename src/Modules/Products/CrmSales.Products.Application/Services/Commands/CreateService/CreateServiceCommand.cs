using CrmSales.SharedKernel.Application;

namespace CrmSales.Products.Application.Services.Commands.CreateService;

public record CreateServiceCommand(
    string Name,
    string? Description,
    string ServiceCode,
    decimal Price,
    string Currency,
    Guid CategoryId,
    string? UnitOfMeasure = null,
    int? EstimatedDurationMinutes = null) : ICommand<Guid>;
