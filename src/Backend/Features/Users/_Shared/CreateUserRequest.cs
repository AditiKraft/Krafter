namespace Backend.Features.Users._Shared;

public class CreateUserRequest
{
    public string Id { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool UpdateTenantEmail { get; set; } = true;

    public bool IsEmailConfirmed { get; set; } = true;

    //bool is external login
    public bool IsExternalLogin { get; set; } = false;
}
