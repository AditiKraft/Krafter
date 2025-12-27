namespace Krafter.Shared.Contracts.Users;

/// <summary>
/// Data transfer object for user role assignment information.
/// </summary>
public class UserRoleDto
{
    public string? RoleId { get; set; }
    public string? RoleName { get; set; }
    public string? Description { get; set; }
    public bool Enabled { get; set; }
}
