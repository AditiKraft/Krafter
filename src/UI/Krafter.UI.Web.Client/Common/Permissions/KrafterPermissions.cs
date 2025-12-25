using System.Collections.ObjectModel;

namespace Krafter.UI.Web.Client.Common.Permissions;

public static class KrafterPermissions
{
    private static readonly KrafterPermission[] AllPermissions = new KrafterPermission[]
    {
        new("View Users", KrafterAction.View, KrafterResource.Users),
        new("Search Users", KrafterAction.Search, KrafterResource.Users),
        new("Create Users", KrafterAction.Create, KrafterResource.Users),
        new("Update Users", KrafterAction.Update, KrafterResource.Users),
        new("Delete Users", KrafterAction.Delete, KrafterResource.Users),
        new("View UserRoles", KrafterAction.View, KrafterResource.UserRoles),
        new("Update UserRoles", KrafterAction.Update, KrafterResource.UserRoles),
        new("View Roles", KrafterAction.View, KrafterResource.Roles),
        new("Create Roles", KrafterAction.Create, KrafterResource.Roles),
        new("Update Roles", KrafterAction.Update, KrafterResource.Roles),
        new("Delete Roles", KrafterAction.Delete, KrafterResource.Roles),
        new("View RoleClaims", KrafterAction.View, KrafterResource.RoleClaims),
        new("Update RoleClaims", KrafterAction.Update, KrafterResource.RoleClaims),
        new("View Notifications", KrafterAction.View, KrafterResource.Notifications, true),
        new("View Tenants", KrafterAction.View, KrafterResource.Tenants, IsRoot: true),
        new("Create Tenants", KrafterAction.Create, KrafterResource.Tenants, IsRoot: true),
        new("Update Tenants", KrafterAction.Update, KrafterResource.Tenants, IsRoot: true),
        new("Delete Tenants", KrafterAction.Delete, KrafterResource.Tenants, IsRoot: true)
    };

    public static IReadOnlyList<KrafterPermission> All { get; } =
        new ReadOnlyCollection<KrafterPermission>(AllPermissions);

    public static IReadOnlyList<KrafterPermission> Root { get; } =
        new ReadOnlyCollection<KrafterPermission>(AllPermissions.Where(p => p.IsRoot).ToArray());

    public static IReadOnlyList<KrafterPermission> Admin { get; } =
        new ReadOnlyCollection<KrafterPermission>(AllPermissions.Where(p => !p.IsRoot).ToArray());

    public static IReadOnlyList<KrafterPermission> Basic { get; } =
        new ReadOnlyCollection<KrafterPermission>(AllPermissions.Where(p => p.IsBasic).ToArray());
}
