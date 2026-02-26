using AditiKraft.Krafter.Contracts.Common.Models;

namespace AditiKraft.Krafter.Contracts.Contracts.Roles;

public class RoleDto : CommonDtoProperty
{
    public string? Name { get; set; } 
    public string? Description { get; set; }
    public List<string>? Permissions { get; set; } = [];
}
