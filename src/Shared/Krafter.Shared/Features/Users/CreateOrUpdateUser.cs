using FluentValidation;
using Krafter.Shared.Features.Users._Shared;

namespace Krafter.Shared.Features.Users;

public sealed class CreateOrUpdateUser
{
    public sealed class Validator : AbstractValidator<CreateUserRequest>
    {
        public Validator()
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
}
