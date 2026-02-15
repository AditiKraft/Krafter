using AditiKraft.Krafter.Shared.Common.Auth;
using AditiKraft.Krafter.Shared.Common.Auth.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Auth._Shared;

public static class RegisterPermissionClaimsClass
{
    public static void RegisterPermissionClaims(AuthorizationOptions options)
    {
        foreach (KrafterPermission permission in KrafterPermissions.All)
        {
            options.AddPolicy(permission.Name,
                policy => policy.RequireClaim(KrafterClaims.Permission, permission.Name));
        }
    }
}
