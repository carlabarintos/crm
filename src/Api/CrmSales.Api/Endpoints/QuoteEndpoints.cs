using CrmSales.Api.Auditing;
using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Api.Notifications;
using CrmSales.Opportunities.Domain.Entities;
using CrmSales.Opportunities.Domain.Repositories;
using CrmSales.Quotes.Domain.Entities;
using CrmSales.Quotes.Domain.Repositories;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.SharedKernel.Messaging;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace CrmSales.Api.Endpoints;

public static class QuoteEndpoints
{
    public static IEndpointRouteBuilder MapQuoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quotes")
            .WithTags("Quotes")
            .RequireAuthorization();

        group.MapGet("/", async (
            IQuoteRepository repo,
            CancellationToken ct,
            [FromQuery] Guid? opportunityId = null,
            [FromQuery] Guid? ownerId = null,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] int limit = 20,
            [FromQuery] string? cursor = null) =>
        {
            // When filtering by opportunity, return full list (used on opportunity detail page)
            if (opportunityId.HasValue && search is null && status is null)
            {
                var all = await repo.GetByOpportunityAsync(opportunityId.Value, ct);
                return Results.Ok(new
                {
                    items = all.Select(q => new
                    {
                        q.Id, q.QuoteNumber, q.OpportunityId,
                        Status = q.Status.ToString(), q.TotalAmount, q.Currency,
                        q.ExpiryDate, q.CreatedAt
                    }),
                    nextCursor = (string?)null,
                    hasMore = false
                });
            }

            var result = await repo.SearchPagedAsync(search, status, opportunityId, limit, cursor, ct);
            return Results.Ok(new
            {
                items = result.Items.Select(q => new
                {
                    q.Id, q.QuoteNumber, q.OpportunityId,
                    Status = q.Status.ToString(), q.TotalAmount, q.Currency,
                    q.ExpiryDate, q.CreatedAt
                }),
                result.NextCursor,
                result.HasMore
            });
        });

        group.MapGet("/summary", async (IQuoteRepository repo, CancellationToken ct) =>
        {
            var s = await repo.GetSummaryAsync(ct);
            return Results.Ok(new
            {
                totalCount    = s.Total,
                draftCount    = s.Draft,
                sentCount     = s.Sent,
                acceptedCount = s.Accepted,
                rejectedCount = s.Rejected,
                expiredCount  = s.Expired,
                sentValue     = s.SentValue,
                acceptedValue = s.AcceptedValue,
                currency      = s.Currency
            });
        });

        group.MapGet("/{id:guid}", async (Guid id, IQuoteRepository repo, CancellationToken ct) =>
        {
            var quote = await repo.GetByIdAsync(id, ct);
            return quote is null ? Results.NotFound() : Results.Ok(new
            {
                quote.Id, quote.QuoteNumber, quote.OpportunityId,
                Status = quote.Status.ToString(),
                quote.SubTotal, quote.DiscountTotal, quote.TotalAmount,
                quote.TaxRateName, quote.TaxRatePercent, quote.TaxAmount, quote.GrandTotal,
                quote.Currency, quote.ExpiryDate, quote.Notes,
                LineItems = quote.LineItems.Select(l => new
                {
                    l.Id, l.ProductId, l.ProductName,
                    l.Quantity, l.UnitPrice, l.DiscountPercent, l.LineTotal
                }),
                quote.CreatedAt, quote.UpdatedAt
            });
        });

        group.MapPost("/", async (
            CreateQuoteRequest req,
            HttpContext http,
            IQuoteRepository repo,
            ITaxRateRepository taxRateRepo,
            INotificationBroadcaster broadcaster,
            IAuditService audit,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            var actor = http.User.FindFirst("preferred_username")?.Value ?? "system";
            var quote = Quote.Create(req.OpportunityId, req.OwnerId, req.Currency, req.ExpiryDate, req.Notes);

            var defaultTax = await taxRateRepo.GetDefaultAsync(ct);
            if (defaultTax is not null && defaultTax.IsActive)
                quote.ApplyTax(defaultTax.Name, defaultTax.Rate);

            await repo.AddAsync(quote, ct);

            var msg = $"Quote {quote.QuoteNumber} created by {actor}";
            await broadcaster.BroadcastAsync(new NotificationEvent(
                "quote.created", "Quote Created", msg,
                quote.Id.ToString(), actor, tenant.TenantId, DateTime.UtcNow), ct);
            await audit.LogAsync(tenant.TenantId, "quote.created", "Quote",
                quote.Id.ToString(), msg, actor, ct);

            return Results.Created($"/api/quotes/{quote.Id}", new { quote.Id, quote.QuoteNumber });
        });

        group.MapPost("/{id:guid}/line-items", async (
            Guid id,
            [FromBody] AddLineItemRequest req,
            IQuoteRepository repo, CancellationToken ct) =>
        {
            var quote = await repo.GetByIdAsync(id, ct);
            if (quote is null) return Results.NotFound();
            quote.AddLineItem(req.ProductId, req.ProductName, req.Quantity, req.UnitPrice, req.DiscountPercent);
            await repo.UpdateAsync(quote, ct);
            return Results.Ok(new { quote.TotalAmount, quote.TaxAmount, quote.GrandTotal });
        });

        group.MapPost("/{id:guid}/send", async (
            Guid id,
            HttpContext http,
            IQuoteRepository repo,
            INotificationBroadcaster broadcaster,
            IAuditService audit,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            var actor = http.User.FindFirst("preferred_username")?.Value ?? "system";
            var quote = await repo.GetByIdAsync(id, ct);
            if (quote is null) return Results.NotFound();
            quote.Send();
            await repo.UpdateAsync(quote, ct);

            var msg = $"Quote {quote.QuoteNumber} sent to customer by {actor}";
            await broadcaster.BroadcastAsync(new NotificationEvent(
                "quote.sent", "Quote Sent", msg,
                quote.Id.ToString(), actor, tenant.TenantId, DateTime.UtcNow), ct);
            await audit.LogAsync(tenant.TenantId, "quote.sent", "Quote",
                quote.Id.ToString(), msg, actor, ct);

            return Results.Ok(new { quote.Id, Status = quote.Status.ToString() });
        }).RequireAuthorization(p => p.RequireRole("Admin", "SalesManager", "SalesRep"));

        group.MapPost("/{id:guid}/accept", async (
            Guid id,
            HttpContext http,
            IQuoteRepository repo,
            IOpportunityRepository oppRepo,
            INotificationBroadcaster broadcaster,
            IAuditService audit,
            ITenantContext tenant,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var actor = http.User.FindFirst("preferred_username")?.Value ?? "system";
            var quote = await repo.GetByIdAsync(id, ct);
            if (quote is null) return Results.NotFound();

            quote.Accept();
            await repo.UpdateAsync(quote, ct);

            var opp = await oppRepo.GetByIdAsync(quote.OpportunityId, ct);
            if (opp is not null && !opp.IsClosed)
            {
                opp.ProgressStage(OpportunityStage.ClosedWon);
                await oppRepo.UpdateAsync(opp, ct);
            }

            var tenantId = http.User.FindFirst("company_id")?.Value ?? "master";

            await bus.PublishAsync(new QuoteAcceptedMessage(
                quote.Id, quote.QuoteNumber, quote.OpportunityId,
                quote.GrandTotal, quote.Currency, quote.OwnerId,
                quote.LineItems
                    .Select(l => new QuoteLineItemMessage(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice))
                    .ToList(),
                TenantId: tenantId,
                TaxRateName: quote.TaxRateName,
                TaxRatePercent: quote.TaxRatePercent));

            var msg = $"Quote {quote.QuoteNumber} accepted by {actor}";
            await broadcaster.BroadcastAsync(new NotificationEvent(
                "quote.accepted", "Quote Accepted", msg,
                quote.Id.ToString(), actor, tenant.TenantId, DateTime.UtcNow), ct);
            await audit.LogAsync(tenant.TenantId, "quote.accepted", "Quote",
                quote.Id.ToString(), msg, actor, ct);

            return Results.Ok(new { quote.Id, Status = quote.Status.ToString() });
        }).RequireAuthorization(p => p.RequireRole("Admin", "SalesManager", "SalesRep"));

        group.MapPost("/{id:guid}/reject", async (
            Guid id,
            [FromBody] RejectQuoteRequest req,
            HttpContext http,
            IQuoteRepository repo,
            IOpportunityRepository oppRepo,
            INotificationBroadcaster broadcaster,
            IAuditService audit,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            var actor = http.User.FindFirst("preferred_username")?.Value ?? "system";
            var quote = await repo.GetByIdAsync(id, ct);
            if (quote is null) return Results.NotFound();
            quote.Reject(req.Reason);
            await repo.UpdateAsync(quote, ct);

            var opp = await oppRepo.GetByIdAsync(quote.OpportunityId, ct);
            if (opp is not null && !opp.IsClosed)
            {
                opp.ProgressStage(OpportunityStage.ClosedLost);
                await oppRepo.UpdateAsync(opp, ct);
            }

            var msg = $"Quote {quote.QuoteNumber} rejected by {actor}" +
                      (string.IsNullOrWhiteSpace(req.Reason) ? "" : $": {req.Reason}");
            await broadcaster.BroadcastAsync(new NotificationEvent(
                "quote.rejected", "Quote Rejected", msg,
                quote.Id.ToString(), actor, tenant.TenantId, DateTime.UtcNow), ct);
            await audit.LogAsync(tenant.TenantId, "quote.rejected", "Quote",
                quote.Id.ToString(), msg, actor, ct);

            return Results.Ok(new { quote.Id, Status = quote.Status.ToString() });
        }).RequireAuthorization(p => p.RequireRole("Admin", "SalesManager"));

        group.MapDelete("/{id:guid}/tax-rate", async (
            Guid id, IQuoteRepository repo, CancellationToken ct) =>
        {
            var quote = await repo.GetByIdAsync(id, ct);
            if (quote is null) return Results.NotFound();
            if (quote.Status != QuoteStatus.Draft)
                return Results.Problem("Can only modify tax on draft quotes.", statusCode: StatusCodes.Status400BadRequest);
            quote.RemoveTax();
            await repo.UpdateAsync(quote, ct);
            return Results.Ok(new { quote.TaxRateName, quote.TaxRatePercent, quote.TaxAmount, quote.GrandTotal });
        });

        return app;
    }
}

record CreateQuoteRequest(Guid OpportunityId, Guid OwnerId, string Currency, DateTime? ExpiryDate, string? Notes);
record AddLineItemRequest(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal DiscountPercent = 0);
record RejectQuoteRequest(string? Reason);
