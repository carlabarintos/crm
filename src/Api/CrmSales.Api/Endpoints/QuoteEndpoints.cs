using System.Net;
using System.Text;
using CrmSales.Api.Auditing;
using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Api.Notifications;
using CrmSales.Opportunities.Domain.Entities;
using CrmSales.Opportunities.Domain.Repositories;
using CrmSales.Quotes.Domain.Entities;
using CrmSales.Quotes.Domain.Repositories;
using CrmSales.Settings.Application.Services;
using CrmSales.Settings.Domain.Enums;
using CrmSales.Settings.Domain.Repositories;
using CrmSales.Users.Domain.Repositories;
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
            var s = await repo.GetSummaryAsync(ct: ct);
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

        group.MapGet("/expiring-soon", async (
            IQuoteRepository repo,
            IUserRepository userRepo,
            HttpContext http,
            CancellationToken ct,
            [FromQuery] int days = 14,
            [FromQuery] int limit = 5) =>
        {
            var isSales = http.User.IsInRole("SalesRep") && !http.User.IsInRole("SalesManager") && !http.User.IsInRole("Admin");
            Guid? ownerId = null;
            if (isSales)
            {
                var keycloakId = http.User.FindFirst("sub")?.Value;
                if (!string.IsNullOrEmpty(keycloakId))
                {
                    var user = await userRepo.GetByKeycloakIdAsync(keycloakId, ct);
                    if (user is not null) ownerId = user.Id;
                }
            }
            var items = await repo.GetExpiringSoonAsync(days, limit, ownerId, ct);
            return Results.Ok(items.Select(q => new
            {
                q.Id,
                q.QuoteNumber,
                q.OpportunityId,
                Status = q.Status.ToString(),
                q.TotalAmount,
                q.Currency,
                q.ExpiryDate,
                DaysLeft = q.ExpiryDate.HasValue
                    ? (int?)(q.ExpiryDate.Value - DateTime.UtcNow).TotalDays
                    : null
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
            IOpportunityRepository oppRepo,
            IEmailTemplateRepository emailTemplateRepo,
            IEmailService emailService,
            INotificationBroadcaster broadcaster,
            IAuditService audit,
            ITenantContext tenant,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("QuoteEndpoints");
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

            bool emailSent = false;
            string? emailNote = null;

            var opp = await oppRepo.GetByIdAsync(quote.OpportunityId, ct);
            if (string.IsNullOrWhiteSpace(opp?.ContactEmail))
            {
                emailNote = "No contact email on this opportunity.";
                logger.LogInformation("Quote email skipped for {QuoteNumber}: {Reason}", quote.QuoteNumber, emailNote);
            }
            else
            {
                var template = await emailTemplateRepo.GetByTypeAsync(EmailTemplateType.QuoteSent, ct);
                if (template is null)
                {
                    emailNote = "No 'Quote Sent' email template configured — set one up in Settings → Email Templates.";
                    logger.LogInformation("Quote email skipped for {QuoteNumber}: {Reason}", quote.QuoteNumber, emailNote);
                }
                else if (!template.IsActive)
                {
                    emailNote = "'Quote Sent' email template is inactive.";
                    logger.LogInformation("Quote email skipped for {QuoteNumber}: {Reason}", quote.QuoteNumber, emailNote);
                }
                else
                {
                    var vars = new Dictionary<string, string>
                    {
                        ["ContactName"] = opp.ContactName,
                        ["QuoteNumber"] = quote.QuoteNumber,
                        ["TotalAmount"] = quote.GrandTotal.ToString("N2"),
                        ["Currency"] = quote.Currency,
                        ["ExpiryDate"] = quote.ExpiryDate?.ToString("MMM d, yyyy") ?? "N/A",
                        ["LineItemsHtml"] = BuildLineItemsHtml(quote)
                    };
                    try
                    {
                        await emailService.SendAsync(
                            opp.ContactEmail, opp.ContactName,
                            TemplateRenderer.Render(template.Subject, vars),
                            TemplateRenderer.Render(template.BodyHtml, vars), ct);
                        emailSent = true;
                        logger.LogInformation("Quote email sent for {QuoteNumber} to {Email}", quote.QuoteNumber, opp.ContactEmail);
                    }
                    catch (Exception ex)
                    {
                        emailNote = ex.Message;
                        logger.LogError(ex, "Failed to send quote email for {QuoteNumber}", quote.QuoteNumber);
                    }
                }
            }

            return Results.Ok(new { quote.Id, Status = quote.Status.ToString(), EmailSent = emailSent, EmailNote = emailNote });
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
                TaxRatePercent: quote.TaxRatePercent,
                ContactId: opp?.ContactId));

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

    private static string BuildLineItemsHtml(Quote quote)
    {
        var sb = new StringBuilder();
        sb.Append("<table style=\"width:100%;border-collapse:collapse;font-size:14px;margin:20px 0\">");
        sb.Append("<thead><tr style=\"background:#1f2937;color:#ffffff\">");
        sb.Append("<th style=\"padding:10px 14px;text-align:left\">Product</th>");
        sb.Append("<th style=\"padding:10px 14px;text-align:right\">Qty</th>");
        sb.Append("<th style=\"padding:10px 14px;text-align:right\">Unit Price</th>");
        sb.Append("<th style=\"padding:10px 14px;text-align:right\">Discount</th>");
        sb.Append("<th style=\"padding:10px 14px;text-align:right\">Line Total</th>");
        sb.Append("</tr></thead><tbody>");

        foreach (var l in quote.LineItems)
        {
            sb.Append("<tr style=\"border-bottom:1px solid #e5e7eb\">");
            sb.Append($"<td style=\"padding:9px 14px\">{WebUtility.HtmlEncode(l.ProductName)}</td>");
            sb.Append($"<td style=\"padding:9px 14px;text-align:right\">{l.Quantity}</td>");
            sb.Append($"<td style=\"padding:9px 14px;text-align:right\">{l.UnitPrice:F2}</td>");
            sb.Append($"<td style=\"padding:9px 14px;text-align:right\">{(l.DiscountPercent > 0 ? $"{l.DiscountPercent:0.##}%" : "—")}</td>");
            sb.Append($"<td style=\"padding:9px 14px;text-align:right;font-weight:600\">{l.LineTotal:F2}</td>");
            sb.Append("</tr>");
        }

        sb.Append("</tbody><tfoot>");
        sb.Append($"<tr style=\"color:#6b7280\"><td colspan=\"4\" style=\"padding:7px 14px;text-align:right\">Subtotal</td><td style=\"padding:7px 14px;text-align:right\">{quote.SubTotal:F2}</td></tr>");

        if (quote.DiscountTotal > 0)
            sb.Append($"<tr style=\"color:#6b7280\"><td colspan=\"4\" style=\"padding:7px 14px;text-align:right\">Discount</td><td style=\"padding:7px 14px;text-align:right;color:#dc2626\">&#8722;{quote.DiscountTotal:F2}</td></tr>");

        sb.Append($"<tr style=\"color:#6b7280\"><td colspan=\"4\" style=\"padding:7px 14px;text-align:right\">Net Total</td><td style=\"padding:7px 14px;text-align:right\">{quote.TotalAmount:F2}</td></tr>");

        if (quote.TaxRatePercent > 0)
            sb.Append($"<tr style=\"color:#6b7280\"><td colspan=\"4\" style=\"padding:7px 14px;text-align:right\">{WebUtility.HtmlEncode(quote.TaxRateName ?? "")} ({quote.TaxRatePercent:0.##}%)</td><td style=\"padding:7px 14px;text-align:right\">+{quote.TaxAmount:F2}</td></tr>");

        sb.Append($"<tr style=\"font-weight:700;background:#eff6ff\"><td colspan=\"4\" style=\"padding:10px 14px;text-align:right\">Grand Total ({WebUtility.HtmlEncode(quote.Currency)})</td><td style=\"padding:10px 14px;text-align:right;font-size:15px;color:#2563eb\">{quote.GrandTotal:F2}</td></tr>");
        sb.Append("</tfoot></table>");

        return sb.ToString();
    }
}

record CreateQuoteRequest(Guid OpportunityId, Guid OwnerId, string Currency, DateTime? ExpiryDate, string? Notes);
record AddLineItemRequest(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal DiscountPercent = 0);
record RejectQuoteRequest(string? Reason);
