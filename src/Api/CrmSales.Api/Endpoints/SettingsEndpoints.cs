using CrmSales.Settings.Application.EmailTemplates.Commands.SaveEmailSettings;
using CrmSales.Settings.Application.EmailTemplates.Commands.UpsertEmailTemplate;
using CrmSales.Settings.Application.EmailTemplates.DTOs;
using CrmSales.Settings.Application.EmailTemplates.Queries.GetEmailSettings;
using CrmSales.Settings.Application.EmailTemplates.Queries.GetEmailTemplates;
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
        // ── Tax Rates ──────────────────────────────────────────────────────────
        var taxGroup = app.MapGroup("/api/settings/tax-rates")
            .WithTags("Settings")
            .RequireAuthorization();

        taxGroup.MapGet("/", async (
            IMessageBus bus,
            CancellationToken ct,
            [FromQuery] bool? isActive = null) =>
        {
            var result = await bus.InvokeAsync<Result<List<TaxRateDto>>>(
                new GetTaxRatesQuery(isActive), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error.Description);
        });

        taxGroup.MapGet("/{id:guid}", async (Guid id, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<TaxRateDto>>(
                new GetTaxRateByIdQuery(id), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error.Description);
        }).WithName("GetTaxRateById");

        taxGroup.MapPost("/", async (CreateTaxRateCommand cmd, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<Guid>>(cmd, ct);
            return result.IsSuccess
                ? Results.CreatedAtRoute("GetTaxRateById", new { id = result.Value }, result.Value)
                : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest);
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        taxGroup.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateTaxRateCommand cmd,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (id != cmd.Id) return Results.BadRequest("ID mismatch.");
            var result = await bus.InvokeAsync<Result>(cmd, ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Description);
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        taxGroup.MapDelete("/{id:guid}", async (Guid id, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(new DeleteTaxRateCommand(id), ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Description);
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        taxGroup.MapPost("/{id:guid}/set-default", async (Guid id, IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(new SetDefaultTaxRateCommand(id), ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Description);
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        // ── Email Templates ────────────────────────────────────────────────────
        var emailGroup = app.MapGroup("/api/settings/email-templates")
            .WithTags("Settings")
            .RequireAuthorization(p => p.RequireRole("Admin"));

        emailGroup.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<List<EmailTemplateDto>>>(new GetEmailTemplatesQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error.Description);
        });

        emailGroup.MapPut("/{type}", async (
            string type,
            [FromBody] UpsertEmailTemplateRequest req,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            if (!Enum.TryParse<CrmSales.Settings.Domain.Enums.EmailTemplateType>(type, true, out var templateType))
                return Results.BadRequest($"Unknown template type '{type}'.");

            var result = await bus.InvokeAsync<Result>(
                new UpsertEmailTemplateCommand(templateType, req.Subject, req.BodyHtml), ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Description);
        });

        // ── Email Config (SMTP) ────────────────────────────────────────────────
        var emailConfigGroup = app.MapGroup("/api/settings/email-config")
            .WithTags("Settings")
            .RequireAuthorization(p => p.RequireRole("Admin"));

        emailConfigGroup.MapGet("/", async (IMessageBus bus, CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result<EmailSettingsDto>>(new GetEmailSettingsQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error.Description);
        });

        emailConfigGroup.MapPut("/", async (
            [FromBody] SaveEmailSettingsCommand cmd,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var result = await bus.InvokeAsync<Result>(cmd, ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Description);
        });

        return app;
    }
}

record UpsertEmailTemplateRequest(string Subject, string BodyHtml);
