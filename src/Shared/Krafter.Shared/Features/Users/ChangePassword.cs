using FluentValidation;

namespace Krafter.Shared.Features.Users;

public sealed class ChangePassword
{
    public sealed class ChangePasswordRequest
    {
        public string Password { get; set; } = default!;
        public string NewPassword { get; set; } = default!;
        public string ConfirmNewPassword { get; set; } = default!;
    }


    internal sealed class Validator : AbstractValidator<ChangePasswordRequest>
    {
        public Validator()
        {
            RuleFor(p => p.Password)
                .NotEmpty().WithMessage("Current password is required");

            RuleFor(p => p.NewPassword)
                .NotEmpty().WithMessage("New password is required")
                .MinimumLength(6).WithMessage("Password must be at least 6 characters");
        }
    }
}
