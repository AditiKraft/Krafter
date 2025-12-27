using FluentValidation;

namespace Krafter.Shared.Features.Users;

public sealed class ResetPassword
{
    public sealed class ResetPasswordRequest
    {
        public string? Email { get; set; }
        public string? Token { get; set; }
        public string? Password { get; set; }
    }


    internal sealed class Validator : AbstractValidator<ResetPasswordRequest>
    {
        public Validator()
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
}
