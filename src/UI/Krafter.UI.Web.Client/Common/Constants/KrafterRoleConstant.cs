using System.Collections.ObjectModel;

namespace Krafter.UI.Web.Client.Common.Constants;

public static class KrafterRoleConstant
{
    public const string Admin = nameof(Admin);
    public const string Basic = nameof(Basic);

    public static IReadOnlyList<string> DefaultRoles { get; } = new ReadOnlyCollection<string>(new[] { Admin, Basic });

    public static bool IsDefault(string roleName) => DefaultRoles.Any(r => r == roleName);
}
