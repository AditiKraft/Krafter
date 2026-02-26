using FluentValidation;

namespace AditiKraft.Krafter.Contracts.Contracts.Users;

public class ResetPasswordRequest
{
    public string? Email { get; set; }
    public string? Token { get; set; }
    public string? Password { get; set; }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(p => p.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(p => p.Token)
            .NotEmpty().WithMessage("Reset token is required");

        RuleFor(p => p.Password)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters");
    }
}
