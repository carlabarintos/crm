using CrmSales.Api.Master;
using CrmSales.Api.Services;
using CrmSales.Settings.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Api.Endpoints;

public static class SubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Subscription Plans (SuperAdmin) ───────────────────────────────────
        var plans = app.MapGroup("/api/subscriptions/plans")
            .WithTags("Subscriptions")
            .RequireAuthorization(p => p.RequireRole("SuperAdmin"));

        plans.MapGet("/", async (MasterDbContext db, CancellationToken ct) =>
        {
            var result = await db.SubscriptionPlans.OrderBy(p => p.PricePhp).ToListAsync(ct);
            return Results.Ok(result.Select(ToDto));
        });

        plans.MapPost("/", async ([FromBody] CreatePlanRequest req, MasterDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest("Name is required.");
            if (!req.IsFree && req.PricePhp <= 0) return Results.BadRequest("Price must be greater than zero for paid plans.");
            if (req.BillingCycleMonths <= 0) return Results.BadRequest("Billing cycle must be at least 1 month.");

            var plan = SubscriptionPlan.Create(req.Name, req.PricePhp, req.BillingCycleMonths,
                req.Description ?? "", req.IsFree);
            ApplyLimits(plan, req);
            db.SubscriptionPlans.Add(plan);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/subscriptions/plans/{plan.Id}", ToDto(plan));
        });

        plans.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdatePlanRequest req, MasterDbContext db, CancellationToken ct) =>
        {
            var plan = await db.SubscriptionPlans.FindAsync([id], ct);
            if (plan is null) return Results.NotFound();

            if (!string.IsNullOrWhiteSpace(req.Name)) plan.Name = req.Name.Trim();
            if (req.IsFree.HasValue) { plan.IsFree = req.IsFree.Value; if (plan.IsFree) plan.PricePhp = 0; }
            if (!plan.IsFree && req.PricePhp.HasValue && req.PricePhp.Value > 0) plan.PricePhp = req.PricePhp.Value;
            if (req.BillingCycleMonths.HasValue && req.BillingCycleMonths.Value > 0) plan.BillingCycleMonths = req.BillingCycleMonths.Value;
            if (req.Description is not null) plan.Description = req.Description;
            if (req.IsActive.HasValue) plan.IsActive = req.IsActive.Value;
            ApplyLimits(plan, req);

            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(plan));
        });

        plans.MapDelete("/{id:guid}", async (Guid id, MasterDbContext db, CancellationToken ct) =>
        {
            var plan = await db.SubscriptionPlans.FindAsync([id], ct);
            if (plan is null) return Results.NotFound();
            db.SubscriptionPlans.Remove(plan);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // ── Company Subscriptions (SuperAdmin) ────────────────────────────────
        var compSubs = app.MapGroup("/api/companies/{companyId:guid}/subscription")
            .WithTags("Subscriptions")
            .RequireAuthorization(p => p.RequireRole("SuperAdmin"));

        compSubs.MapGet("/", async (Guid companyId, MasterDbContext db, CancellationToken ct) =>
        {
            var sub = await db.CompanySubscriptions.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);
            if (sub is null) return Results.NotFound();
            var plan = await db.SubscriptionPlans.FindAsync([sub.PlanId], ct);
            return Results.Ok(ToSubscriptionDto(sub, plan));
        });

        compSubs.MapPost("/", async (
            Guid companyId,
            [FromBody] AssignSubscriptionRequest req,
            MasterDbContext db,
            IHttpContextAccessor httpCtx,
            CancellationToken ct) =>
        {
            var company = await db.Companies.FindAsync([companyId], ct);
            if (company is null) return Results.NotFound("Company not found.");

            var plan = await db.SubscriptionPlans.FindAsync([req.PlanId], ct);
            if (plan is null) return Results.NotFound("Plan not found.");

            var actor = httpCtx.HttpContext?.User.FindFirst("preferred_username")?.Value ?? "superadmin";
            var now = DateTime.UtcNow;

            var sub = await db.CompanySubscriptions.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);
            if (sub is null)
            {
                sub = new CompanySubscription
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    PlanId = req.PlanId,
                    Status = SubscriptionStatus.Active,
                    PeriodStart = now,
                    PeriodEnd = now.AddMonths(plan.BillingCycleMonths),
                    Notes = req.Notes,
                    UpdatedAt = now,
                    UpdatedBy = actor
                };
                db.CompanySubscriptions.Add(sub);
            }
            else
            {
                sub.PlanId = req.PlanId;
                sub.Status = SubscriptionStatus.Active;
                sub.PeriodStart = now;
                sub.PeriodEnd = now.AddMonths(plan.BillingCycleMonths);
                sub.Notes = req.Notes;
                sub.UpdatedAt = now;
                sub.UpdatedBy = actor;
            }

            company.UpdateSubscriptionStatus(SubscriptionStatus.Active);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToSubscriptionDto(sub, plan));
        });

        compSubs.MapPut("/status", async (
            Guid companyId,
            [FromBody] UpdateSubscriptionStatusRequest req,
            MasterDbContext db,
            IHttpContextAccessor httpCtx,
            CancellationToken ct) =>
        {
            var validStatuses = new[] { SubscriptionStatus.Active, SubscriptionStatus.Suspended, SubscriptionStatus.Trialing, SubscriptionStatus.Expired };
            if (!validStatuses.Contains(req.Status))
                return Results.BadRequest($"Invalid status. Valid values: {string.Join(", ", validStatuses)}");

            var sub = await db.CompanySubscriptions.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);
            if (sub is null) return Results.NotFound("No subscription found for this company.");

            var company = await db.Companies.FindAsync([companyId], ct);
            var actor = httpCtx.HttpContext?.User.FindFirst("preferred_username")?.Value ?? "superadmin";

            sub.Status = req.Status;
            sub.Notes = req.Notes ?? sub.Notes;
            sub.UpdatedAt = DateTime.UtcNow;
            sub.UpdatedBy = actor;
            company?.UpdateSubscriptionStatus(req.Status);

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // ── Invoices (SuperAdmin) ─────────────────────────────────────────────
        var invoices = app.MapGroup("/api/invoices")
            .WithTags("Invoices")
            .RequireAuthorization(p => p.RequireRole("SuperAdmin"));

        invoices.MapGet("/", async (
            MasterDbContext db,
            Guid? companyId,
            string? status,
            CancellationToken ct) =>
        {
            var query = db.Invoices.AsQueryable();
            if (companyId.HasValue) query = query.Where(i => i.CompanyId == companyId.Value);
            if (!string.IsNullOrEmpty(status)) query = query.Where(i => i.Status == status);

            var items = await query.OrderByDescending(i => i.IssuedAt).ToListAsync(ct);
            var companyIds = items.Select(i => i.CompanyId).Distinct().ToList();
            var companies = await db.Companies.Where(c => companyIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

            return Results.Ok(items.Select(i => ToInvoiceDto(i, companies.GetValueOrDefault(i.CompanyId))));
        });

        invoices.MapPost("/", async (
            [FromBody] CreateInvoiceRequest req,
            MasterDbContext db,
            IHttpContextAccessor httpCtx,
            CancellationToken ct) =>
        {
            var company = await db.Companies.FindAsync([req.CompanyId], ct);
            if (company is null) return Results.NotFound("Company not found.");
            if (req.AmountPhp <= 0) return Results.BadRequest("Amount must be greater than zero.");

            SubscriptionPlan? plan = null;
            if (req.PlanId.HasValue)
            {
                plan = await db.SubscriptionPlans.FindAsync([req.PlanId.Value], ct);
                if (plan is null) return Results.NotFound("Plan not found.");
            }

            var actor = httpCtx.HttpContext?.User.FindFirst("preferred_username")?.Value ?? "superadmin";
            var invoiceNumber = await GenerateInvoiceNumberAsync(db, ct);

            var invoice = Invoice.Create(req.CompanyId, req.PlanId, req.AmountPhp,
                req.Description, req.DueDate, actor, invoiceNumber);
            if (!string.IsNullOrWhiteSpace(req.Notes)) invoice.Notes = req.Notes;

            db.Invoices.Add(invoice);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/invoices/{invoice.Id}", ToInvoiceDto(invoice, company));
        });

        invoices.MapGet("/{id:guid}", async (Guid id, MasterDbContext db, CancellationToken ct) =>
        {
            var invoice = await db.Invoices.FindAsync([id], ct);
            if (invoice is null) return Results.NotFound();
            var company = await db.Companies.FindAsync([invoice.CompanyId], ct);
            return Results.Ok(ToInvoiceDto(invoice, company));
        });

        invoices.MapPost("/{id:guid}/send", async (
            Guid id,
            [FromBody] SendInvoiceRequest req,
            MasterDbContext db,
            InvoiceEmailService emailSvc,
            CancellationToken ct) =>
        {
            var invoice = await db.Invoices.FindAsync([id], ct);
            if (invoice is null) return Results.NotFound();
            if (invoice.Status == InvoiceStatus.Cancelled) return Results.Conflict("Cannot send a cancelled invoice.");

            var company = await db.Companies.FindAsync([invoice.CompanyId], ct);
            if (company is null) return Results.NotFound("Company not found.");

            SubscriptionPlan? plan = null;
            if (invoice.SubscriptionPlanId.HasValue)
                plan = await db.SubscriptionPlans.FindAsync([invoice.SubscriptionPlanId.Value], ct);

            var paymentInstructions = req.PaymentInstructions ??
                "GCash / Maya / Bank Transfer\nPlease use the invoice number as your payment reference.";

            try
            {
                await emailSvc.SendInvoiceAsync(invoice, company, plan, req.ToEmail, paymentInstructions, ct);
                invoice.MarkSent();
                await db.SaveChangesAsync(ct);
                return Results.Ok(new { message = "Invoice sent." });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to send email: {ex.Message}", statusCode: 502);
            }
        });

        invoices.MapPost("/{id:guid}/mark-paid", async (
            Guid id,
            MasterDbContext db,
            IHttpContextAccessor httpCtx,
            CancellationToken ct) =>
        {
            var invoice = await db.Invoices.FindAsync([id], ct);
            if (invoice is null) return Results.NotFound();
            if (invoice.Status == InvoiceStatus.Paid) return Results.Conflict("Invoice is already paid.");
            if (invoice.Status == InvoiceStatus.Cancelled) return Results.Conflict("Cannot mark a cancelled invoice as paid.");

            invoice.MarkPaid();

            var actor = httpCtx.HttpContext?.User.FindFirst("preferred_username")?.Value ?? "superadmin";
            var now = DateTime.UtcNow;

            if (invoice.SubscriptionPlanId.HasValue)
            {
                var plan = await db.SubscriptionPlans.FindAsync([invoice.SubscriptionPlanId.Value], ct);
                if (plan is not null)
                {
                    var sub = await db.CompanySubscriptions.FirstOrDefaultAsync(s => s.CompanyId == invoice.CompanyId, ct);
                    if (sub is null)
                    {
                        sub = new CompanySubscription
                        {
                            Id = Guid.NewGuid(),
                            CompanyId = invoice.CompanyId,
                            PlanId = plan.Id,
                            Status = SubscriptionStatus.Active,
                            PeriodStart = now,
                            PeriodEnd = now.AddMonths(plan.BillingCycleMonths),
                            UpdatedAt = now,
                            UpdatedBy = actor
                        };
                        db.CompanySubscriptions.Add(sub);
                    }
                    else
                    {
                        sub.PlanId = plan.Id;
                        sub.Status = SubscriptionStatus.Active;
                        sub.PeriodStart = now;
                        sub.PeriodEnd = now.AddMonths(plan.BillingCycleMonths);
                        sub.UpdatedAt = now;
                        sub.UpdatedBy = actor;
                    }

                    var company = await db.Companies.FindAsync([invoice.CompanyId], ct);
                    company?.UpdateSubscriptionStatus(SubscriptionStatus.Active);
                }
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { message = "Invoice marked as paid. Subscription activated." });
        });

        invoices.MapPost("/{id:guid}/mark-sent", async (Guid id, MasterDbContext db, CancellationToken ct) =>
        {
            var invoice = await db.Invoices.FindAsync([id], ct);
            if (invoice is null) return Results.NotFound();
            if (invoice.Status == InvoiceStatus.Cancelled) return Results.Conflict("Cannot mark a cancelled invoice as sent.");

            invoice.MarkSent();
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { message = "Invoice marked as sent." });
        });

        invoices.MapPost("/{id:guid}/cancel", async (Guid id, MasterDbContext db, CancellationToken ct) =>
        {
            var invoice = await db.Invoices.FindAsync([id], ct);
            if (invoice is null) return Results.NotFound();
            if (invoice.Status == InvoiceStatus.Paid) return Results.Conflict("Cannot cancel a paid invoice.");

            invoice.Cancel();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // ── Public plans endpoint (no auth) ──────────────────────────────────
        app.MapGet("/api/subscriptions/plans/public", async (MasterDbContext db, CancellationToken ct) =>
        {
            var result = await db.SubscriptionPlans
                .Where(p => p.IsActive)
                .OrderBy(p => p.PricePhp)
                .ToListAsync(ct);
            return Results.Ok(result.Select(ToDto));
        }).AllowAnonymous().WithTags("Subscriptions");

        // ── Company Admin billing view ─────────────────────────────────────────
        var billing = app.MapGroup("/api/billing")
            .WithTags("Billing")
            .RequireAuthorization();

        billing.MapGet("/my-subscription", async (
            MasterDbContext db,
            IHttpContextAccessor httpCtx,
            CancellationToken ct) =>
        {
            var slug = httpCtx.HttpContext?.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(slug)) return Results.NotFound();

            var company = await db.Companies.FirstOrDefaultAsync(c => c.Slug == slug, ct);
            if (company is null) return Results.NotFound();

            var sub = await db.CompanySubscriptions.FirstOrDefaultAsync(s => s.CompanyId == company.Id, ct);
            if (sub is null) return Results.Ok(new { subscriptionStatus = (string?)null });

            var plan = await db.SubscriptionPlans.FindAsync([sub.PlanId], ct);
            return Results.Ok(ToSubscriptionDto(sub, plan));
        });

        billing.MapGet("/my-invoices", async (
            MasterDbContext db,
            IHttpContextAccessor httpCtx,
            CancellationToken ct) =>
        {
            var slug = httpCtx.HttpContext?.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(slug)) return Results.Ok(Array.Empty<object>());

            var company = await db.Companies.FirstOrDefaultAsync(c => c.Slug == slug, ct);
            if (company is null) return Results.Ok(Array.Empty<object>());

            var items = await db.Invoices
                .Where(i => i.CompanyId == company.Id)
                .OrderByDescending(i => i.IssuedAt)
                .ToListAsync(ct);

            return Results.Ok(items.Select(i => ToInvoiceDto(i, company)));
        });

        return app;
    }

    private static async Task<string> GenerateInvoiceNumberAsync(MasterDbContext db, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"INV-{year}-";
        var lastNumber = await db.Invoices
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync(ct);

        var next = 1;
        if (lastNumber is not null && int.TryParse(lastNumber[prefix.Length..], out var parsed))
            next = parsed + 1;

        return $"{prefix}{next:D4}";
    }

    private static void ApplyLimits(SubscriptionPlan plan, ILimitRequest req)
    {
        plan.MaxUsers = req.MaxUsers;
        plan.MaxContacts = req.MaxContacts;
        plan.MaxProducts = req.MaxProducts;
        plan.MaxServices = req.MaxServices;
        plan.MaxCategories = req.MaxCategories;
        plan.MaxOpportunities = req.MaxOpportunities;
        plan.MaxQuotes = req.MaxQuotes;
        plan.MaxOrders = req.MaxOrders;
        plan.AdditionalFeatures = string.IsNullOrWhiteSpace(req.AdditionalFeatures)
            ? null : req.AdditionalFeatures.Trim();
    }

    private static object ToDto(SubscriptionPlan p) => new
    {
        p.Id, p.Name, p.PricePhp, p.BillingCycleMonths, p.Description,
        p.MaxUsers, p.MaxContacts, p.MaxProducts, p.MaxServices,
        p.MaxCategories, p.MaxOpportunities, p.MaxQuotes, p.MaxOrders,
        p.AdditionalFeatures, p.IsFree, p.IsActive, p.CreatedAt,
        FeatureList = p.GetFeatureList()
    };

    private static object ToSubscriptionDto(CompanySubscription s, SubscriptionPlan? plan) => new
    {
        s.Id, s.CompanyId, s.PlanId, s.Status,
        s.PeriodStart, s.PeriodEnd, s.Notes, s.UpdatedAt, s.UpdatedBy,
        Plan = plan is null ? null : ToDto(plan)
    };

    private static object ToInvoiceDto(Invoice i, Company? company) => new
    {
        i.Id, i.InvoiceNumber, i.CompanyId,
        CompanyName = company?.Name ?? "",
        i.SubscriptionPlanId, i.Status,
        i.AmountPhp, i.Description,
        i.IssuedAt, i.DueDate, i.PaidAt, i.SentAt, i.Notes, i.CreatedBy
    };
}

interface ILimitRequest
{
    int? MaxUsers { get; }
    int? MaxContacts { get; }
    int? MaxProducts { get; }
    int? MaxServices { get; }
    int? MaxCategories { get; }
    int? MaxOpportunities { get; }
    int? MaxQuotes { get; }
    int? MaxOrders { get; }
    string? AdditionalFeatures { get; }
}

record CreatePlanRequest(
    string Name, decimal PricePhp, int BillingCycleMonths, string? Description, bool IsFree = false,
    int? MaxUsers = null, int? MaxContacts = null, int? MaxProducts = null, int? MaxServices = null,
    int? MaxCategories = null, int? MaxOpportunities = null, int? MaxQuotes = null, int? MaxOrders = null,
    string? AdditionalFeatures = null) : ILimitRequest;

record UpdatePlanRequest(
    string? Name, decimal? PricePhp, int? BillingCycleMonths, string? Description, bool? IsFree, bool? IsActive,
    int? MaxUsers = null, int? MaxContacts = null, int? MaxProducts = null, int? MaxServices = null,
    int? MaxCategories = null, int? MaxOpportunities = null, int? MaxQuotes = null, int? MaxOrders = null,
    string? AdditionalFeatures = null) : ILimitRequest;
record AssignSubscriptionRequest(Guid PlanId, string? Notes);
record UpdateSubscriptionStatusRequest(string Status, string? Notes);
record CreateInvoiceRequest(Guid CompanyId, Guid? PlanId, decimal AmountPhp, string Description, DateTime DueDate, string? Notes);
record SendInvoiceRequest(string ToEmail, string? PaymentInstructions);
