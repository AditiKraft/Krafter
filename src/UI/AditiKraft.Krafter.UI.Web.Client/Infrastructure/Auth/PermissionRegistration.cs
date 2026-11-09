using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.UI.Web.Client.Common.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public static class PermissionRegistration
{
    public static void RegisterPermissionClaims(AuthorizationOptions options)
    {
        foreach (PermissionDefinition permission in PermissionCatalog.All)
        {
            // Display policies may use cached claims. Backend operation policies use live database checks.
            options.AddPolicy(UiPermissionPolicy.NameFor(permission.Action, permission.Resource),
                policy => policy.RequireAuthenticatedUser().RequireClaim(AppClaimTypes.Permission, permission.Name));
        }
    }
}
