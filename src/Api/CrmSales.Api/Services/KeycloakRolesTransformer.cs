using System.Security.Claims;
using System.Text.Json;
using CrmSales.SharedKernel.Authorization;
using CrmSales.SharedKernel.MultiTenancy;
using CrmSales.Users.Domain.Repositories;
using Microsoft.AspNetCore.Authentication;

namespace CrmSales.Api.Services;

public class UserClaimsTransformation(IServiceProvider sp) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated is not true)
            return principal;

        var identity = (ClaimsIdentity)principal.Identity!;

        // Keycloak system roles (SuperAdmin, Admin)
        var realmAccess = principal.FindFirst("realm_access")?.Value;
        if (realmAccess is not null)
        {
            using var doc = JsonDocument.Parse(realmAccess);
            if (doc.RootElement.TryGetProperty("roles", out var rolesEl))
            {
                foreach (var role in rolesEl.EnumerateArray())
                {
                    var roleName = role.GetString();
                    if (roleName is not null)
                        identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                }
            }
        }

        // Admin gets all permissions automatically — no DB lookup needed
        if (principal.IsInRole("Admin"))
        {
            foreach (var perm in Permissions.All)
                identity.AddClaim(new Claim("permission", perm));
            return principal;
        }

        // DB-backed permission claims for regular users
        var keycloakId = principal.FindFirst("sub")?.Value;
        var tenantId = principal.FindFirst("company_id")?.Value;

        if (keycloakId is not null && tenantId is not null)
        {
            using var scope = sp.CreateScope();
            var tenantCtx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantCtx.TenantId = tenantId;

            var roleRepo = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
            var permissions = await roleRepo.GetPermissionsForUserAsync(keycloakId);
            foreach (var perm in permissions)
                identity.AddClaim(new Claim("permission", perm));
        }

        return principal;
    }
}
