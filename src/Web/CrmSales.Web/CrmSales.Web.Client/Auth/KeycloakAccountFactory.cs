using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;

namespace CrmSales.Web.Client.Auth;

public class KeycloakAccountFactory(IAccessTokenProviderAccessor accessor)
    : AccountClaimsPrincipalFactory<RemoteUserAccount>(accessor)
{
    public override async ValueTask<ClaimsPrincipal> CreateUserAsync(
        RemoteUserAccount account, RemoteAuthenticationUserOptions options)
    {
        var user = await base.CreateUserAsync(account, options);
        if (user.Identity?.IsAuthenticated != true) return user;

        var identity = (ClaimsIdentity)user.Identity;

        // ID token doesn't carry realm_access — decode the access token instead
        var tokenResult = await accessor.TokenProvider.RequestAccessToken();
        if (!tokenResult.TryGetToken(out var accessToken))
        {
            Console.WriteLine("[KeycloakFactory] Could not retrieve access token");
            return user;
        }

        var parts = accessToken.Value.Split('.');
        if (parts.Length != 3)
        {
            Console.WriteLine("[KeycloakFactory] Access token is not a valid JWT");
            return user;
        }

        var payload = parts[1];
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));

        Console.WriteLine($"[KeycloakFactory] Access token payload: {doc.RootElement}");

        if (doc.RootElement.TryGetProperty("realm_access", out var realmAccess) &&
            realmAccess.TryGetProperty("roles", out var rolesEl))
        {
            foreach (var role in rolesEl.EnumerateArray())
            {
                var roleName = role.GetString();
                if (roleName is not null)
                {
                    identity.AddClaim(new Claim(identity.RoleClaimType, roleName));
                    Console.WriteLine($"[KeycloakFactory] Added role: {roleName}");
                }
            }
        }
        else
        {
            Console.WriteLine("[KeycloakFactory] realm_access.roles not found in access token");
        }

        if (doc.RootElement.TryGetProperty("company_id", out var companyEl))
        {
            var companyId = companyEl.GetString();
            if (companyId is not null)
            {
                identity.AddClaim(new Claim("company_id", companyId));
                Console.WriteLine($"[KeycloakFactory] Added company_id: {companyId}");
            }
        }

        return user;
    }
}
