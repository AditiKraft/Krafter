using FluentValidation;

namespace AditiKraft.Krafter.Contracts.Contracts.Users;

public class CreateUserRequest
{
    public string? Id { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public List<string> Roles { get; set; } = [];
    public bool UpdateTenantEmail { get; set; } = true;
    public bool IsEmailConfirmed { get; set; } = true;
    public bool IsExternalLogin { get; set; } = false;
}

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(p => p.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100).WithMessage("First name cannot exceed 100 characters");

        RuleFor(p => p.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters");

        RuleFor(p => p.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters");

        RuleFor(p => p.PhoneNumber)
            .MaximumLength(20).When(p => !string.IsNullOrWhiteSpace(p.PhoneNumber))
            .WithMessage("Phone number cannot exceed 20 characters");
    }
}
