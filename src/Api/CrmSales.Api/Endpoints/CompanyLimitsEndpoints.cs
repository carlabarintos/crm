using CrmSales.Api.Master;
using Microsoft.EntityFrameworkCore;

namespace CrmSales.Api.Endpoints;

public static class CompanyLimitsEndpoints
{
    public static IEndpointRouteBuilder MapCompanyLimitsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/companies/{companyId:guid}/limits")
            .WithTags("Companies")
            .RequireAuthorization(p => p.RequireRole("SuperAdmin"));

        group.MapGet("/", async (Guid companyId, MasterDbContext db, CancellationToken ct) =>
        {
            var company = await db.Companies.FindAsync([companyId], ct);
            if (company is null) return Results.NotFound();

            var limits = await db.CompanyLimits
                .FirstOrDefaultAsync(l => l.CompanyId == companyId, ct);

            return Results.Ok(new CompanyLimitsDto(
                CompanyId: companyId,
                CompanyName: company.Name,
                MaxProducts: limits?.MaxProducts,
                MaxCategories: limits?.MaxCategories,
                MaxServices: limits?.MaxServices,
                MaxContacts: limits?.MaxContacts,
                MaxOpportunities: limits?.MaxOpportunities,
                MaxQuotes: limits?.MaxQuotes,
                MaxOrders: limits?.MaxOrders,
                MaxUsers: limits?.MaxUsers,
                UpdatedAt: limits?.UpdatedAt,
                UpdatedBy: limits?.UpdatedBy));
        });

        group.MapPut("/", async (
            Guid companyId,
            SetCompanyLimitsRequest req,
            MasterDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            var company = await db.Companies.FindAsync([companyId], ct);
            if (company is null) return Results.NotFound();

            var actor = http.User.FindFirst("preferred_username")?.Value ?? "superadmin";

            var limits = await db.CompanyLimits
                .FirstOrDefaultAsync(l => l.CompanyId == companyId, ct);

            if (limits is null)
            {
                limits = new CompanyLimits
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId
                };
                db.CompanyLimits.Add(limits);
            }

            limits.MaxProducts = req.MaxProducts;
            limits.MaxCategories = req.MaxCategories;
            limits.MaxServices = req.MaxServices;
            limits.MaxContacts = req.MaxContacts;
            limits.MaxOpportunities = req.MaxOpportunities;
            limits.MaxQuotes = req.MaxQuotes;
            limits.MaxOrders = req.MaxOrders;
            limits.MaxUsers = req.MaxUsers;
            limits.UpdatedAt = DateTime.UtcNow;
            limits.UpdatedBy = actor;

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return app;
    }
}

record CompanyLimitsDto(
    Guid CompanyId,
    string CompanyName,
    int? MaxProducts,
    int? MaxCategories,
    int? MaxServices,
    int? MaxContacts,
    int? MaxOpportunities,
    int? MaxQuotes,
    int? MaxOrders,
    int? MaxUsers,
    DateTime? UpdatedAt,
    string? UpdatedBy);

record SetCompanyLimitsRequest(
    int? MaxProducts,
    int? MaxCategories,
    int? MaxServices,
    int? MaxContacts,
    int? MaxOpportunities,
    int? MaxQuotes,
    int? MaxOrders,
    int? MaxUsers);
