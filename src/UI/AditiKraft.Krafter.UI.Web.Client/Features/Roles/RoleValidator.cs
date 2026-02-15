using FluentValidation;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Roles;

public class RoleValidator : AbstractValidator<CreateOrUpdateRoleRequest>
{
    public RoleValidator()
    {
        RuleFor(p => p.Name)
            .NotNull().NotEmpty().WithMessage("You must enter Name")
            .MaximumLength(13)
            .WithMessage("Name cannot be longer than 13 characters")
            ;


        RuleFor(p => p.Description)
            .NotNull().NotEmpty().WithMessage("You must enter Description")
            .MaximumLength(100)
            .WithMessage("Description cannot be longer than 100 characters")
            ;
    }
}
