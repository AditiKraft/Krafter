using Krafter.Shared.Common.Models;

namespace Krafter.Shared.Features.Users;

public sealed class GetUsers
{
    public sealed class UserDto : CommonDtoProperty
    {
        public string? Id { get; set; }
        public string? UserName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public bool EmailConfirmed { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
