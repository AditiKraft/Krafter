using AditiKraft.Krafter.Shared.Common.Models;

namespace AditiKraft.Krafter.Shared.Contracts.Roles;

public class RoleDto : CommonDtoProperty
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public List<string>? Permissions { get; set; } = [];
}
