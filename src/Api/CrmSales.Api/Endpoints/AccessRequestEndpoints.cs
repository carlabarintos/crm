using CrmSales.Api.Master;
using CrmSales.Api.MultiTenancy;
using CrmSales.Api.Notifications;
using CrmSales.Api.Services;
using Microsoft.AspNetCore.Mvc;
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
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name)
                || string.IsNullOrWhiteSpace(req.Company)
                || string.IsNullOrWhiteSpace(req.Email))
                return Results.BadRequest("Name, company and email are required.");

            var request = AccessRequest.Create(
                req.Name.Trim(), req.Company.Trim(), req.Email.Trim(),
                req.Phone?.Trim(), req.Message?.Trim());
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
        }).AllowAnonymous();

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
                    r.Message, r.Status, r.RequestedAt, r.ReviewedAt))
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

record SubmitAccessRequest(string Name, string Company, string Email, string? Phone, string? Message);
record AccessRequestDto(Guid Id, string Name, string Company, string Email, string? Phone,
    string? Message, string Status, DateTime RequestedAt, DateTime? ReviewedAt);
record ApproveResult(Guid CompanyId, string CompanyName, string Slug,
    string KeycloakId, string Email, string TempPassword);
