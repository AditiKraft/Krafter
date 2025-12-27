using FluentValidation;

namespace Krafter.Shared.Contracts.Users;

/// <summary>
/// Request model for changing a user's password.
/// </summary>
public class ChangePasswordRequest
{
    public string Password { get; set; } = default!;
    public string NewPassword { get; set; } = default!;
    public string ConfirmNewPassword { get; set; } = default!;
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(p => p.Password)
            .NotEmpty().WithMessage("Current password is required");

        RuleFor(p => p.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters");

        RuleFor(p => p.ConfirmNewPassword)
            .Equal(p => p.NewPassword).WithMessage("Passwords do not match");
    }
}
