using CrmSales.Api.Auditing;
using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Api.Notifications;
using CrmSales.Opportunities.Domain.Entities;
using CrmSales.Opportunities.Domain.Repositories;
using CrmSales.SharedKernel.Application;
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
            IOpportunityRepository repo,
            CancellationToken ct,
            [FromQuery] string? search = null,
            [FromQuery] OpportunityStage? stage = null,
            [FromQuery] Guid? ownerId = null,
            [FromQuery] int limit = 20,
            [FromQuery] string? cursor = null) =>
        {
            var result = await repo.SearchAsync(search, stage, ownerId, limit, cursor, ct);
            return Results.Ok(new
            {
                items = result.Items.Select(o => new
                {
                    o.Id, o.Name, o.AccountName, o.ContactId, o.ContactName,
                    Stage = o.Stage.ToString(), o.EstimatedValue, o.Currency,
                    o.Probability, o.ExpectedCloseDate, o.OwnerId,
                    o.CreatedAt, o.UpdatedAt
                }),
                result.NextCursor,
                result.HasMore
            });
        });

        group.MapGet("/summary", async (IOpportunityRepository repo, CancellationToken ct) =>
        {
            var s = await repo.GetSummaryAsync(ct);
            var closed = s.Won + s.Lost;
            var winRate = closed > 0 ? $"{(decimal)s.Won / closed * 100:N0}%" : "—";
            return Results.Ok(new
            {
                totalCount     = s.Total,
                openCount      = s.Total - s.Won - s.Lost,
                wonCount       = s.Won,
                lostCount      = s.Lost,
                winRate,
                pipelineValue  = s.PipelineValue,
                weightedValue  = s.WeightedValue,
                avgDaysToClose = Math.Round(s.AvgDaysToClose, 0),
                currency       = s.Currency,
                byStage        = s.ByStage.Select(st => new { stage = st.Stage, count = st.Count, value = st.Value })
            });
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

        group.MapPost("/", async (
            CreateOpportunityRequest req,
            HttpContext http,
            IOpportunityRepository repo,
            INotificationBroadcaster broadcaster,
            IAuditService audit,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            var actor = http.User.FindFirst("preferred_username")?.Value ?? "system";
            var opp = Opportunity.Create(
                req.Name, req.AccountName, req.ContactName,
                req.ContactEmail, req.ContactPhone,
                req.EstimatedValue, req.Currency,
                req.ExpectedCloseDate, req.Description, req.OwnerId,
                req.ContactId);
            await repo.AddAsync(opp, ct);

            var msg = $"'{opp.Name}' opportunity created by {actor}";
            await broadcaster.BroadcastAsync(new NotificationEvent(
                "opportunity.created", "Opportunity Created", msg,
                opp.Id.ToString(), actor, tenant.TenantId, DateTime.UtcNow), ct);
            await audit.LogAsync(tenant.TenantId, "opportunity.created", "Opportunity",
                opp.Id.ToString(), msg, actor, ct);

            return Results.Created($"/api/opportunities/{opp.Id}", new { opp.Id, opp.Name });
        });

        group.MapPatch("/{id:guid}/stage", async (
            Guid id,
            [FromBody] ChangeStageRequest req,
            HttpContext http,
            IOpportunityRepository repo,
            INotificationBroadcaster broadcaster,
            IAuditService audit,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            var actor = http.User.FindFirst("preferred_username")?.Value ?? "system";
            var opp = await repo.GetByIdAsync(id, ct);
            if (opp is null) return Results.NotFound();
            var oldStage = opp.Stage.ToString();
            opp.ProgressStage(req.Stage);
            await repo.UpdateAsync(opp, ct);

            string type, title, msg;
            if (opp.Stage == OpportunityStage.ClosedWon)
            {
                type = "opportunity.won";
                title = "Opportunity Won!";
                msg = $"'{opp.Name}' won! {opp.EstimatedValue:N0} {opp.Currency} by {actor}";
            }
            else
            {
                type = "opportunity.stage_changed";
                title = "Stage Updated";
                msg = $"'{opp.Name}' moved from {oldStage} to {opp.Stage} by {actor}";
            }

            await broadcaster.BroadcastAsync(new NotificationEvent(
                type, title, msg, opp.Id.ToString(), actor, tenant.TenantId, DateTime.UtcNow), ct);
            await audit.LogAsync(tenant.TenantId, type, "Opportunity",
                opp.Id.ToString(), msg, actor, ct);

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
