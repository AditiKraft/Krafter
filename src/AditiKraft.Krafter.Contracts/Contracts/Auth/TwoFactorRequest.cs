using FluentValidation;

namespace AditiKraft.Krafter.Contracts.Contracts.Auth;

public class TwoFactorRequest
{
    public string ChallengeId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool UseRecoveryCode { get; set; }
}

public sealed class TwoFactorRequestValidator : AbstractValidator<TwoFactorRequest>
{
    public TwoFactorRequestValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(128);
    }
}

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
        RuleFor(x => x.Password).MaximumLength(1024);
        RuleFor(x => x.Code).MaximumLength(128);
    }
}
