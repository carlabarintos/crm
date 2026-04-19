using CrmSales.Api.Master;
using CrmSales.Api.MultiTenancy;
using CrmSales.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace CrmSales.Api.Endpoints;

public static class CompanyEndpoints
{
    public static IEndpointRouteBuilder MapCompanyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies")
            .WithTags("Companies")
            .RequireAuthorization(p => p.RequireRole("SuperAdmin"));

        group.MapGet("/", async (MasterDbContext db, CancellationToken ct) =>
        {
            var companies = db.Companies.OrderBy(c => c.Name).Select(c => new
            {
                c.Id, c.Name, c.Slug, c.IsActive, c.CreatedAt
            });
            return Results.Ok(companies);
        });

        group.MapPost("/", async (
            [FromBody] CreateCompanyRequest req,
            MasterDbContext db,
            TenantProvisioner provisioner,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Slug))
                return Results.BadRequest("Name and slug are required.");

            var slug = req.Slug.ToLowerInvariant().Trim();
            if (!Regex.IsMatch(slug, @"^[a-z0-9][a-z0-9\-]{0,49}$"))
                return Results.BadRequest("Slug must be lowercase alphanumeric with hyphens, max 50 chars.");

            if (db.Companies.Any(c => c.Slug == slug))
                return Results.Conflict($"Slug '{slug}' is already taken.");

            var company = Company.Create(req.Name.Trim(), slug);
            db.Companies.Add(company);
            await db.SaveChangesAsync(ct);

            await provisioner.ProvisionAsync(slug);

            return Results.Created($"/api/companies/{company.Id}", new
            {
                company.Id, company.Name, company.Slug
            });
        });

        group.MapPost("/{id:guid}/admin", async (
            Guid id,
            [FromBody] CreateCompanyAdminRequest req,
            MasterDbContext db,
            KeycloakAdminClient keycloak,
            CancellationToken ct) =>
        {
            var company = await db.Companies.FindAsync([id], ct);
            if (company is null) return Results.NotFound();

            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
                return Results.BadRequest("First name, last name and email are required.");

            string keycloakId;
            var attrs = new Dictionary<string, string[]> { ["company_id"] = [company.Slug] };
            try { keycloakId = await keycloak.CreateUserAsync(req.Email, req.FirstName, req.LastName, attrs); }
            catch (Exception ex) { return Results.Problem($"Failed to create user in Keycloak: {ex.Message}", statusCode: 502); }

            var tempPassword = Guid.NewGuid().ToString("N")[..12];
            try { await keycloak.SetTemporaryPasswordAsync(keycloakId, tempPassword); } catch { }
            try { await keycloak.AssignRoleAsync(keycloakId, "Admin"); } catch { }

            return Results.Ok(new { keycloakId, req.Email, TempPassword = tempPassword });
        });

        return app;
    }
}

record CreateCompanyRequest(string Name, string Slug);
record CreateCompanyAdminRequest(string FirstName, string LastName, string Email);
