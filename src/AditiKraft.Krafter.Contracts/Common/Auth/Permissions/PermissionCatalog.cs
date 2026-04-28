using System.Collections.ObjectModel;

namespace AditiKraft.Krafter.Contracts.Common.Auth.Permissions;

public static class PermissionCatalog
{
    private static readonly PermissionDefinition[] AllPermissions =
    [
        new("View Users", PermissionAction.View, PermissionResource.Users),
        new("Search Users", PermissionAction.Search, PermissionResource.Users),
        new("Create Users", PermissionAction.Create, PermissionResource.Users),
        new("Update Users", PermissionAction.Update, PermissionResource.Users),
        new("Delete Users", PermissionAction.Delete, PermissionResource.Users),
        new("View UserRoles", PermissionAction.View, PermissionResource.UserRoles),
        new("Update UserRoles", PermissionAction.Update, PermissionResource.UserRoles),
        new("View Roles", PermissionAction.View, PermissionResource.Roles),
        new("Create Roles", PermissionAction.Create, PermissionResource.Roles),
        new("Update Roles", PermissionAction.Update, PermissionResource.Roles),
        new("Delete Roles", PermissionAction.Delete, PermissionResource.Roles),
        new("View RoleClaims", PermissionAction.View, PermissionResource.RoleClaims),
        new("Update RoleClaims", PermissionAction.Update, PermissionResource.RoleClaims),
        new("View Notifications", PermissionAction.View, PermissionResource.Notifications, true),
        new("View Tenants", PermissionAction.View, PermissionResource.Tenants, IsRoot: true),
        new("Create Tenants", PermissionAction.Create, PermissionResource.Tenants, IsRoot: true),
        new("Update Tenants", PermissionAction.Update, PermissionResource.Tenants, IsRoot: true),
        new("Delete Tenants", PermissionAction.Delete, PermissionResource.Tenants, IsRoot: true)
    ];

    public static IReadOnlyList<PermissionDefinition> All { get; } =
        new ReadOnlyCollection<PermissionDefinition>(AllPermissions);

    public static IReadOnlyList<PermissionDefinition> Root { get; } =
        new ReadOnlyCollection<PermissionDefinition>(AllPermissions.Where(p => p.IsRoot).ToArray());

    public static IReadOnlyList<PermissionDefinition> Admin { get; } =
        new ReadOnlyCollection<PermissionDefinition>(AllPermissions.Where(p => !p.IsRoot).ToArray());

    public static IReadOnlyList<PermissionDefinition> Basic { get; } =
        new ReadOnlyCollection<PermissionDefinition>(AllPermissions.Where(p => p.IsBasic).ToArray());
}
