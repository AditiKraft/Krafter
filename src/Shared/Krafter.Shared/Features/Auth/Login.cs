using FluentValidation;

namespace Krafter.Shared.Features.Auth;

public sealed class GetToken
{
    public sealed class TokenRequestInput
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool IsExternalLogin { get; set; } = false;
        public string Code { get; set; }
    }


    public class TokenRequestValidator : AbstractValidator<TokenRequestInput>
    {
        public TokenRequestValidator()
        {
            RuleFor(p => p.Email).Cascade(CascadeMode.Stop)
                .NotEmpty()
                .EmailAddress()
                .WithMessage("Invalid Email Address.");

            RuleFor(p => p.Password).Cascade(CascadeMode.Stop)
                .NotEmpty();
        }
    }
}
