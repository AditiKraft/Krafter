namespace Krafter.Shared.Features.Users;

public sealed class GetUserRoles
{
    public sealed class UserRoleDto
    {
        public string? RoleId { get; set; }
        public string? RoleName { get; set; }
        public string? Description { get; set; }
        public bool Enabled { get; set; }
    }
}
