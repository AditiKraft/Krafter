namespace AditiKraft.Krafter.Contracts.Common;

public static class KrafterRoute
{
    public const string Roles = "roles";
    public const string Tenants = "tenants";
    public const string Tokens = "tokens";
    public const string Users = "users";
    public const string AppInfo = "app-info";
    public const string ExternalAuth = "external-auth";
}

public static class RouteSegment
{
    public const string ById = "{id}";

    public const string UserRoles = "{userId}/roles";

    public const string RolePermissions = "{roleId}/permissions";

    public const string ByRole = "by-role/{roleId}";

    public const string Permissions = "permissions";

    public const string Refresh = "refresh";

    public const string Current = "current";

    public const string Logout = "logout";

    public const string Google = "google";

    public const string ChangePassword = "change-password";

    public const string ForgotPassword = "forgot-password";

    public const string ResetPassword = "reset-password";

    public const string SeedData = "seed-data";
}
