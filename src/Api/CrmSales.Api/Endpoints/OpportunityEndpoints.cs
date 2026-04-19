using CrmSales.Opportunities.Domain.Entities;
using CrmSales.Opportunities.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CrmSales.Api.Endpoints;

public static class OpportunityEndpoints
{
    public static IEndpointRouteBuilder MapOpportunityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/opportunities")
            .WithTags("Opportunities")
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] string? search,
            [FromQuery] OpportunityStage? stage,
            [FromQuery] Guid? ownerId,
            IOpportunityRepository repo,
            CancellationToken ct) =>
        {
            var opportunities = await repo.SearchAsync(search, stage, ownerId, ct);
            return Results.Ok(opportunities.Select(o => new
            {
                o.Id, o.Name, o.AccountName, o.ContactId, o.ContactName,
                Stage = o.Stage.ToString(), o.EstimatedValue, o.Currency,
                o.Probability, o.ExpectedCloseDate, o.OwnerId,
                o.CreatedAt, o.UpdatedAt
            }));
        });

        group.MapGet("/{id:guid}", async (Guid id, IOpportunityRepository repo, CancellationToken ct) =>
        {
            var opp = await repo.GetByIdAsync(id, ct);
            return opp is null ? Results.NotFound() : Results.Ok(new
            {
                opp.Id, opp.Name, opp.AccountName, opp.ContactId, opp.ContactName,
                opp.ContactEmail, opp.ContactPhone,
                Stage = opp.Stage.ToString(), opp.EstimatedValue, opp.Currency,
                opp.Probability, opp.ExpectedCloseDate, opp.Description, opp.OwnerId,
                Activities = opp.Activities.Select(a => new { a.Id, a.Type, a.Notes, a.OccurredAt }),
                opp.CreatedAt, opp.UpdatedAt
            });
        });

        group.MapPost("/", async (CreateOpportunityRequest req, IOpportunityRepository repo, CancellationToken ct) =>
        {
            var opp = Opportunity.Create(
                req.Name, req.AccountName, req.ContactName,
                req.ContactEmail, req.ContactPhone,
                req.EstimatedValue, req.Currency,
                req.ExpectedCloseDate, req.Description, req.OwnerId,
                req.ContactId);
            await repo.AddAsync(opp, ct);
            return Results.Created($"/api/opportunities/{opp.Id}", new { opp.Id, opp.Name });
        });

        group.MapPatch("/{id:guid}/stage", async (
            Guid id,
            [FromBody] ChangeStageRequest req,
            IOpportunityRepository repo,
            CancellationToken ct) =>
        {
            var opp = await repo.GetByIdAsync(id, ct);
            if (opp is null) return Results.NotFound();
            opp.ProgressStage(req.Stage);
            await repo.UpdateAsync(opp, ct);
            return Results.Ok(new { opp.Id, Stage = opp.Stage.ToString(), opp.Probability });
        });

        group.MapPost("/{id:guid}/activities", async (
            Guid id,
            [FromBody] AddActivityRequest req,
            IOpportunityRepository repo,
            CancellationToken ct) =>
        {
            var opp = await repo.GetByIdAsync(id, ct);
            if (opp is null) return Results.NotFound();
            opp.AddActivity(req.Type, req.Notes, req.PerformedById);
            await repo.UpdateAsync(opp, ct);
            return Results.Created($"/api/opportunities/{opp.Id}", null);
        });

        return app;
    }
}

record CreateOpportunityRequest(
    string Name, string AccountName, string ContactName,
    string? ContactEmail, string? ContactPhone,
    decimal EstimatedValue, string Currency,
    DateTime? ExpectedCloseDate, string? Description, Guid OwnerId,
    Guid? ContactId = null);

record ChangeStageRequest(OpportunityStage Stage);
record AddActivityRequest(string Type, string Notes, Guid PerformedById);
