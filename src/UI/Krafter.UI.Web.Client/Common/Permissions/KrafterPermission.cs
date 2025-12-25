namespace Krafter.UI.Web.Client.Common.Permissions;

public record KrafterPermission(
    string Description,
    string Action,
    string Resource,
    bool IsBasic = false,
    bool IsRoot = false)
{
    public string Name => NameFor(Action, Resource);
    public static string NameFor(string action, string resource) => $"Permissions.{resource}.{action}";
}
