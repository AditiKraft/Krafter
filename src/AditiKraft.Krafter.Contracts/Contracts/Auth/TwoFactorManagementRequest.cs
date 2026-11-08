using AditiKraft.Krafter.Contracts.Common.Auth;
using FluentValidation;

namespace AditiKraft.Krafter.Contracts.Contracts.Auth;

public class TwoFactorManagementRequest
{
    public string Password { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool UseRecoveryCode { get; set; }
}

public sealed class TwoFactorManagementRequestValidator : AbstractValidator<TwoFactorManagementRequest>
{
    public TwoFactorManagementRequestValidator()
    {
        RuleFor(x => x.Password).MaximumLength(PasswordLimits.MaximumLength);
        RuleFor(x => x.Code).MaximumLength(128);
    }
}
