using CrmSales.Api.Master;
using CrmSales.Api.MultiTenancy;
using CrmSales.Api.Notifications;
using CrmSales.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CrmSales.Api.Endpoints;

public static class AccessRequestEndpoints
{
    public static IEndpointRouteBuilder MapAccessRequestEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Public: submit a request ──────────────────────────────────────────
        app.MapPost("/api/access-requests", async (
            [FromBody] SubmitAccessRequest req,
            MasterDbContext db,
            INotificationBroadcaster broadcaster,
            IConfiguration config,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name)
                || string.IsNullOrWhiteSpace(req.Company)
                || string.IsNullOrWhiteSpace(req.Email))
                return Results.BadRequest("Name, company and email are required.");

            if (req.Name.Length > 100)
                return Results.BadRequest("Name must be 100 characters or fewer.");
            if (req.Company.Length > 150)
                return Results.BadRequest("Company name must be 150 characters or fewer.");
            if (req.Email.Length > 150)
                return Results.BadRequest("Email must be 150 characters or fewer.");
            if (req.Phone?.Length > 50)
                return Results.BadRequest("Phone must be 50 characters or fewer.");
            if (req.Message?.Length > 2000)
                return Results.BadRequest("Message must be 2000 characters or fewer.");
            if (req.SelectedPlanName?.Length > 100)
                return Results.BadRequest("Plan name must be 100 characters or fewer.");

            var normalizedEmail = req.Email.Trim().ToLowerInvariant();
            var duplicateExists = await db.AccessRequests
                .AnyAsync(r => r.Email.ToLower() == normalizedEmail && r.Status == "Pending", ct);
            if (duplicateExists)
                return Results.Conflict("An access request from this email is already pending review.");

            var maxPending = config.GetValue<int>("AccessRequests:MaxPendingCount", 100);
            var pendingCount = await db.AccessRequests.CountAsync(r => r.Status == "Pending", ct);
            if (pendingCount >= maxPending)
                return Results.Problem(
                    "We are not accepting new access requests at this time. Please try again later.",
                    statusCode: 503);

            var request = AccessRequest.Create(
                req.Name.Trim(), req.Company.Trim(), req.Email.Trim(),
                req.Phone?.Trim(), req.Message?.Trim(),
                req.SelectedPlanId, req.SelectedPlanName?.Trim());
            db.AccessRequests.Add(request);
            await db.SaveChangesAsync(ct);

            await broadcaster.BroadcastAsync(new NotificationEvent(
                Type: "access_request.submitted",
                Title: "New Access Request",
                Message: $"{request.Name} from {request.Company} has requested access.",
                EntityId: request.Id.ToString(),
                Actor: request.Email,
                TenantId: "master",
                OccurredAt: request.RequestedAt), ct);

            return Results.Created($"/api/access-requests/{request.Id}", new { request.Id });
        }).AllowAnonymous().RequireRateLimiting("public-form");

        // ── SuperAdmin: list requests ─────────────────────────────────────────
        var admin = app.MapGroup("/api/access-requests")
            .RequireAuthorization(p => p.RequireRole("SuperAdmin"));

        admin.MapGet("/", async (
            MasterDbContext db,
            string? status,
            CancellationToken ct) =>
        {
            var query = db.AccessRequests.AsQueryable();
            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);
            var results = await query
                .OrderByDescending(r => r.RequestedAt)
                .Select(r => new AccessRequestDto(
                    r.Id, r.Name, r.Company, r.Email, r.Phone,
                    r.Message, r.Status, r.RequestedAt, r.ReviewedAt,
                    r.SelectedPlanId, r.SelectedPlanName))
                .ToListAsync(ct);
            return Results.Ok(results);
        });

        // ── SuperAdmin: approve → create company + admin user ─────────────────
        admin.MapPost("/{id:guid}/approve", async (
            Guid id,
            MasterDbContext db,
            TenantProvisioner provisioner,
            KeycloakAdminClient keycloak,
            CancellationToken ct) =>
        {
            var request = await db.AccessRequests.FindAsync([id], ct);
            if (request is null) return Results.NotFound();
            if (request.Status != "Pending")
                return Results.Conflict("Request has already been reviewed.");

            var slug = GenerateSlug(request.Company);
            var baseSlug = slug;
            var suffix = 2;
            while (db.Companies.Any(c => c.Slug == slug))
                slug = $"{baseSlug}-{suffix++}";

            var company = Company.Create(request.Company, slug);
            db.Companies.Add(company);
            await db.SaveChangesAsync(ct);
            await provisioner.ProvisionAsync(slug);

            var nameParts = request.Name.Trim().Split(' ', 2);
            var firstName = nameParts[0];
            var lastName = nameParts.Length > 1 ? nameParts[1] : nameParts[0];
            var attrs = new Dictionary<string, string[]> { ["company_id"] = [slug] };

            string keycloakId;
            try { keycloakId = await keycloak.CreateUserAsync(request.Email, firstName, lastName, attrs); }
            catch (Exception ex)
            {
                return Results.Problem($"Company created but Keycloak user creation failed: {ex.Message}", statusCode: 502);
            }

            var tempPassword = Guid.NewGuid().ToString("N")[..12];
            try { await keycloak.SetTemporaryPasswordAsync(keycloakId, tempPassword); } catch { }
            try { await keycloak.AssignRoleAsync(keycloakId, "Admin"); } catch { }

            request.Status = "Approved";
            request.ReviewedAt = DateTime.UtcNow;

            if (request.SelectedPlanId.HasValue)
            {
                var plan = await db.SubscriptionPlans.FindAsync([request.SelectedPlanId.Value], ct);
                if (plan is not null)
                {
                    var now = DateTime.UtcNow;
                    db.CompanySubscriptions.Add(new CompanySubscription
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = company.Id,
                        PlanId = plan.Id,
                        Status = plan.IsFree ? SubscriptionStatus.Active : SubscriptionStatus.Trialing,
                        PeriodStart = now,
                        PeriodEnd = now.AddMonths(plan.BillingCycleMonths > 0 ? plan.BillingCycleMonths : 1),
                        Notes = $"Auto-assigned from access request (plan: {plan.Name})",
                        UpdatedAt = now,
                        UpdatedBy = "system"
                    });
                }
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(new ApproveResult(
                company.Id, company.Name, company.Slug,
                keycloakId, request.Email, tempPassword));
        });

        // ── SuperAdmin: reject ────────────────────────────────────────────────
        admin.MapPost("/{id:guid}/reject", async (
            Guid id,
            MasterDbContext db,
            CancellationToken ct) =>
        {
            var request = await db.AccessRequests.FindAsync([id], ct);
            if (request is null) return Results.NotFound();
            if (request.Status != "Pending")
                return Results.Conflict("Request has already been reviewed.");

            request.Status = "Rejected";
            request.ReviewedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { request.Id, request.Status });
        });

        return app;
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant().Trim();
        slug = Regex.Replace(slug, @"[^a-z0-9\s\-]", "");
        slug = Regex.Replace(slug, @"[\s\-]+", "-");
        slug = slug.Trim('-');
        if (slug.Length > 50) slug = slug[..50].TrimEnd('-');
        return string.IsNullOrEmpty(slug) ? "company" : slug;
    }
}

record SubmitAccessRequest(string Name, string Company, string Email, string? Phone, string? Message, Guid? SelectedPlanId, string? SelectedPlanName);
record AccessRequestDto(Guid Id, string Name, string Company, string Email, string? Phone,
    string? Message, string Status, DateTime RequestedAt, DateTime? ReviewedAt,
    Guid? SelectedPlanId, string? SelectedPlanName);
record ApproveResult(Guid CompanyId, string CompanyName, string Slug,
    string KeycloakId, string Email, string TempPassword);
