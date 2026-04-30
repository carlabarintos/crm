using System.Security.Claims;

namespace CrmSales.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static bool HasPermission(this ClaimsPrincipal user, string permission)
        => user.HasClaim("permission", permission);
}
