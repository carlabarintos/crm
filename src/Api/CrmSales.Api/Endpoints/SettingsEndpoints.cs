using CrmSales.Settings.Application.TaxRates.Commands.CreateTaxRate;
using CrmSales.Settings.Application.TaxRates.Commands.DeleteTaxRate;
using CrmSales.Settings.Application.TaxRates.Commands.SetDefaultTaxRate;
using CrmSales.Settings.Application.TaxRates.Commands.UpdateTaxRate;
using CrmSales.Settings.Application.TaxRates.DTOs;
using CrmSales.Settings.Application.TaxRates.Queries.GetTaxRateById;
using CrmSales.Settings.Application.TaxRates.Queries.GetTaxRates;
using CrmSales.SharedKernel;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace CrmSales.Api.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings/tax-rates")
            .WithTags("Settings")
            .RequireAuthorization();

        group.MapGet("/", async (
            IMessageBus bus,
            CancellationToken ct,
            [FromQuery] bool? isActive = null) =>
        {
            var result = await bus.InvokeAsync<Result<List<TaxRateDto>>>(
                new GetTaxRatesQuery(isActive), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error.Description);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<TaxRateDto>>(
                new GetTaxRateByIdQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error.Description);
        }).WithName("GetTaxRateById");

        group.MapPost("/", async (CreateTaxRateCommand cmd, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<Guid>>(cmd, ct);
            return result.IsSuccess
                ? Results.CreatedAtRoute("GetTaxRateById", new { id = result.Value }, result.Value)
                : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateTaxRateCommand cmd,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (id != cmd.Id) return Results.BadRequest("ID mismatch.");
            var result = await bus.InvokeAsync<Result>(cmd, ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Description);
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        group.MapDelete("/{id:guid}", async (Guid id, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(new DeleteTaxRateCommand(id), ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Description);
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        group.MapPost("/{id:guid}/set-default", async (Guid id, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(new SetDefaultTaxRateCommand(id), ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Description);
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        return app;
    }
}
