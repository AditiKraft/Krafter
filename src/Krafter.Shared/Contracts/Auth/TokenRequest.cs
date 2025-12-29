using FluentValidation;

namespace Krafter.Shared.Contracts.Auth;

public class TokenRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsExternalLogin { get; set; } = false;
    public string? Code { get; set; }
}

public class TokenRequestValidator : AbstractValidator<TokenRequest>
{
    public TokenRequestValidator()
    {
        RuleFor(p => p.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("Invalid Email Address.");

        RuleFor(p => p.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .When(p => !p.IsExternalLogin);
    }
}
