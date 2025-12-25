using Backend.Common.Models;

namespace Backend.Features.Roles._Shared;

public class RoleDto : CommonDtoProperty
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    public List<string>? Permissions { get; set; } = new();
}
