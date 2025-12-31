using FluentValidation;
using Krafter.Shared.Contracts.Tenants;


namespace Krafter.UI.Web.Client.Features.Tenants;

public class TenantValidator : AbstractValidator<CreateOrUpdateTenantRequest>
{
    public TenantValidator()
    {
        RuleFor(p => p.Name)
            .NotNull().NotEmpty().WithMessage("You must enter Name")
            .MaximumLength(40)
            .WithMessage("Name cannot be longer than 40 characters");

        RuleFor(p => p.AdminEmail)
            .NotEmpty()
            .NotEmpty()
            .EmailAddress()
            ;

        RuleFor(p => p.Identifier)
            .NotEmpty()
            .NotEmpty()
            .MaximumLength(10)
            ;


        RuleFor(p => p.IsActive)
            .NotNull()
            ;

        RuleFor(p => p.ValidUpto)
            .NotNull();
    }
}
