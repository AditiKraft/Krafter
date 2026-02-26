using AditiKraft.Krafter.Contracts.Common.Models;

namespace AditiKraft.Krafter.Contracts.Contracts.Users;

public class UserDto : CommonDtoProperty
{
    public string? UserName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? PhoneNumber { get; set; }
}
