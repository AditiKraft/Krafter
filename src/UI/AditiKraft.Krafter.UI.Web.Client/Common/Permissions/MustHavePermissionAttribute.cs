using AditiKraft.Krafter.Shared.Common.Auth.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace AditiKraft.Krafter.UI.Web.Client.Common.Permissions;

public class MustHavePermissionAttribute : AuthorizeAttribute
{
    public MustHavePermissionAttribute(string action, string resource)
    {
        Policy = KrafterPermission.NameFor(action, resource);
    }
}
