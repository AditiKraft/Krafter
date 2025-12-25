using FluentValidation;
using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client.Common.Validators;

namespace Krafter.UI.Web.Client.Features.Tenants;

public class TenantValidator : AbstractValidator<CreateOrUpdateTenantRequestInput>
{
    public TenantValidator()
    {
        RuleFor(p => p.Name)
            .NotNull().NotEmpty().WithMessage("You must enter Name")
            .MaximumLength(40)
            .WithMessage("Name cannot be longer than 40 characters").When(c =>
                string.IsNullOrWhiteSpace(c.Id) || FluentValidationConfig.IsRunningOnUI);

        RuleFor(p => p.AdminEmail)
            .NotEmpty()
            .NotEmpty()
            .EmailAddress()
            .When(c => string.IsNullOrWhiteSpace(c.Id) || FluentValidationConfig.IsRunningOnUI);

        RuleFor(p => p.Identifier)
            .NotEmpty()
            .NotEmpty()
            .MaximumLength(10)
            .When(c => string.IsNullOrWhiteSpace(c.Id) || FluentValidationConfig.IsRunningOnUI);


        RuleFor(p => p.IsActive)
            .NotNull()
            .When(c => string.IsNullOrWhiteSpace(c.Id) || FluentValidationConfig.IsRunningOnUI);

        RuleFor(p => p.ValidUpto)
            .NotNull()
            .When(c => string.IsNullOrWhiteSpace(c.Id) || FluentValidationConfig.IsRunningOnUI);
    }
}
