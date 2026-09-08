using AditiKraft.Krafter.Contracts.Common.Auth;
using Microsoft.AspNetCore.Authorization;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public static class PermissionRegistration
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
