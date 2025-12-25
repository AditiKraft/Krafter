using Microsoft.AspNetCore.Authorization;

namespace Krafter.UI.Web.Client.Common.Permissions;

public class MustHavePermissionAttribute : AuthorizeAttribute
{
    public MustHavePermissionAttribute(string action, string resource)
    {
        Policy = KrafterPermission.NameFor(action, resource);
    }
}
