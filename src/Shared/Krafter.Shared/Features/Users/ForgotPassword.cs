using FluentValidation;

namespace Krafter.Shared.Features.Users;

public sealed class ForgotPassword
{
    public sealed class ForgotPasswordRequest
    {
        public string Email { get; set; } = default!;
    }


    internal sealed class Validator : AbstractValidator<ForgotPasswordRequest>
    {
        public Validator()
        {
            RuleFor(p => p.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format");
        }
    }
}
