using CrmSales.Opportunities.Domain.Entities;
using CrmSales.Opportunities.Domain.Repositories;
using CrmSales.Quotes.Domain.Entities;
using CrmSales.Quotes.Domain.Repositories;
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
            [FromQuery] Guid? opportunityId,
            [FromQuery] Guid? ownerId,
            IQuoteRepository repo, CancellationToken ct) =>
        {
            var quotes = opportunityId.HasValue
                ? await repo.GetByOpportunityAsync(opportunityId.Value, ct)
                : ownerId.HasValue
                    ? await repo.GetByOwnerAsync(ownerId.Value, ct)
                    : await repo.GetAllAsync(ct);

            return Results.Ok(quotes.Select(q => new
            {
                q.Id, q.QuoteNumber, q.OpportunityId,
                Status = q.Status.ToString(), q.TotalAmount, q.Currency,
                q.ExpiryDate, q.CreatedAt
            }));
        });

        group.MapGet("/{id:guid}", async (Guid id, IQuoteRepository repo, CancellationToken ct) =>
        {
            var quote = await repo.GetByIdAsync(id, ct);
            return quote is null ? Results.NotFound() : Results.Ok(new
            {
                quote.Id, quote.QuoteNumber, quote.OpportunityId,
                Status = quote.Status.ToString(),
                quote.SubTotal, quote.DiscountTotal, quote.TotalAmount,
                quote.Currency, quote.ExpiryDate, quote.Notes,
                LineItems = quote.LineItems.Select(l => new
                {
                    l.Id, l.ProductId, l.ProductName,
                    l.Quantity, l.UnitPrice, l.DiscountPercent, l.LineTotal
                }),
                quote.CreatedAt, quote.UpdatedAt
            });
        });

        group.MapPost("/", async (CreateQuoteRequest req, IQuoteRepository repo, CancellationToken ct) =>
        {
            var quote = Quote.Create(req.OpportunityId, req.OwnerId, req.Currency, req.ExpiryDate, req.Notes);
            await repo.AddAsync(quote, ct);
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
            return Results.Ok(new { quote.TotalAmount });
        });

        group.MapPost("/{id:guid}/send", async (Guid id, IQuoteRepository repo, CancellationToken ct) =>
        {
            var quote = await repo.GetByIdAsync(id, ct);
            if (quote is null) return Results.NotFound();
            quote.Send();
            await repo.UpdateAsync(quote, ct);
            return Results.Ok(new { quote.Id, Status = quote.Status.ToString() });
        });

        // Accept a quote — persists the change, auto-closes the linked opportunity (ClosedWon),
        // then publishes QuoteAcceptedMessage to RabbitMQ so Orders can auto-create an Order.
        group.MapPost("/{id:guid}/accept", async (
            Guid id,
            IQuoteRepository repo,
            IOpportunityRepository oppRepo,
            IMessageBus bus,
            CancellationToken ct) =>
        {
            var quote = await repo.GetByIdAsync(id, ct);
            if (quote is null) return Results.NotFound();

            quote.Accept();
            await repo.UpdateAsync(quote, ct);

            // Auto-close the linked opportunity as Won
            var opp = await oppRepo.GetByIdAsync(quote.OpportunityId, ct);
            if (opp is not null && !opp.IsClosed)
            {
                opp.ProgressStage(OpportunityStage.ClosedWon);
                await oppRepo.UpdateAsync(opp, ct);
            }

            // Publish async integration event → Wolverine routes to RabbitMQ → Orders creates Order
            await bus.PublishAsync(new QuoteAcceptedMessage(
                quote.Id,
                quote.QuoteNumber,
                quote.OpportunityId,
                quote.TotalAmount,
                quote.Currency,
                quote.OwnerId,
                quote.LineItems
                    .Select(l => new QuoteLineItemMessage(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice))
                    .ToList()));

            return Results.Ok(new { quote.Id, Status = quote.Status.ToString() });
        });

        // Reject a quote — persists the change and auto-closes the linked opportunity as Lost.
        group.MapPost("/{id:guid}/reject", async (
            Guid id,
            [FromBody] RejectQuoteRequest req,
            IQuoteRepository repo,
            IOpportunityRepository oppRepo,
            CancellationToken ct) =>
        {
            var quote = await repo.GetByIdAsync(id, ct);
            if (quote is null) return Results.NotFound();
            quote.Reject(req.Reason);
            await repo.UpdateAsync(quote, ct);

            // Auto-close the linked opportunity as Lost
            var opp = await oppRepo.GetByIdAsync(quote.OpportunityId, ct);
            if (opp is not null && !opp.IsClosed)
            {
                opp.ProgressStage(OpportunityStage.ClosedLost);
                await oppRepo.UpdateAsync(opp, ct);
            }

            return Results.Ok(new { quote.Id, Status = quote.Status.ToString() });
        });

        return app;
    }
}

record CreateQuoteRequest(Guid OpportunityId, Guid OwnerId, string Currency, DateTime? ExpiryDate, string? Notes);
record AddLineItemRequest(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal DiscountPercent = 0);
record RejectQuoteRequest(string? Reason);
