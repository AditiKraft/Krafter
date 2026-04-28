using AditiKraft.Krafter.Contracts.Common.Auth;
using Microsoft.AspNetCore.Authorization;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Auth.Common;

public static class RegisterPermissionClaimsClass
{
    public static void RegisterPermissionClaims(AuthorizationOptions options)
    {
        foreach (PermissionDefinition permission in PermissionCatalog.All)
        {
            options.AddPolicy(permission.Name,
                policy => policy.RequireClaim(AppClaimTypes.Permission, permission.Name));
        }
    }
}


