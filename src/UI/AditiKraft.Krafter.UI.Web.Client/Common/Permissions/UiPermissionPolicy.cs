namespace AditiKraft.Krafter.UI.Web.Client.Common.Permissions;

public static class UiPermissionPolicy
{
    public static string NameFor(string action, string resource) =>
        $"UI.{PermissionDefinition.NameFor(action, resource)}";
}
