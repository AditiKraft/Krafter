using AditiKraft.Krafter.Contracts.Common.Auth;
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
