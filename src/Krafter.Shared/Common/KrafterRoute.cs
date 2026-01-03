namespace Krafter.Shared.Common;

/// <summary>
/// Base route prefixes for API endpoints (e.g., "users", "roles").
/// Used in both Backend MapGroup() and UI Refit interfaces.
/// </summary>
public static class KrafterRoute
{
    public const string Roles = "roles";
    public const string Tenants = "tenants";
    public const string Tokens = "tokens";
    public const string Users = "users";
    public const string AppInfo = "app-info";
    public const string ExternalAuth = "external-auth";
}

/// <summary>
/// REST-compliant route segments for API endpoints.
/// 
/// Standard REST patterns used:
/// - GET  /resources              → List all (empty segment)
/// - GET  /resources/{id}         → Get single by ID
/// - POST /resources              → Create new (empty segment)
/// - PUT  /resources/{id}         → Full update
/// - DELETE /resources/{id}       → Delete
/// 
/// Custom action patterns (RPC-style for complex operations):
/// - POST /resources/action-name  → Custom action
/// 
/// Use with KrafterRoute: $"/{KrafterRoute.Users}/{RouteSegment.ById}"
/// </summary>
public static class RouteSegment
{
    // ═══════════════════════════════════════════════════════════════
    // STANDARD REST - Resource operations
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>GET /resources/{id}, PUT /resources/{id}, DELETE /resources/{id}</summary>
    public const string ById = "{id}";
    
    // ═══════════════════════════════════════════════════════════════
    // NESTED RESOURCES - Related data
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>GET /users/{userId}/roles - Get roles for a user</summary>
    public const string UserRoles = "{userId}/roles";
    
    /// <summary>GET /roles/{roleId}/permissions - Get permissions for a role</summary>
    public const string RolePermissions = "{roleId}/permissions";
    
    /// <summary>GET /users/by-role/{roleId} - Filter users by role</summary>
    public const string ByRole = "by-role/{roleId}";
    
    /// <summary>GET /users/permissions - Get current user's permissions</summary>
    public const string Permissions = "permissions";
    
    // ═══════════════════════════════════════════════════════════════
    // AUTH ACTIONS - Token operations
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>POST /tokens/refresh</summary>
    public const string Refresh = "refresh";
    
    /// <summary>POST /tokens/logout</summary>
    public const string Logout = "logout";
    
    /// <summary>POST /external-auth/google</summary>
    public const string Google = "google";
    
    // ═══════════════════════════════════════════════════════════════
    // USER ACTIONS - Password management
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>POST /users/change-password</summary>
    public const string ChangePassword = "change-password";
    
    /// <summary>POST /users/forgot-password</summary>
    public const string ForgotPassword = "forgot-password";
    
    /// <summary>POST /users/reset-password</summary>
    public const string ResetPassword = "reset-password";
    
    // ═══════════════════════════════════════════════════════════════
    // TENANT ACTIONS
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>POST /tenants/seed-data</summary>
    public const string SeedData = "seed-data";
}
