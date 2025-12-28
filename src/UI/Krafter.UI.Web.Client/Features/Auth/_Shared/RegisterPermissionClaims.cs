using Krafter.Shared.Common.Auth.Permissions;
using Krafter.UI.Web.Client.Common.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Krafter.UI.Web.Client.Features.Auth._Shared;

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
