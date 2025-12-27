using Krafter.Shared.Common.Models;

namespace Krafter.Shared.Contracts.Roles;

/// <summary>
/// Data transfer object for role information.
/// </summary>
public class RoleDto : CommonDtoProperty
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public List<string>? Permissions { get; set; } = [];
}
