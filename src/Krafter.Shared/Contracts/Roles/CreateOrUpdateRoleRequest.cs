using FluentValidation;

namespace Krafter.Shared.Contracts.Roles;

public class CreateOrUpdateRoleRequest
{
    public string? Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = [];
}

public class CreateOrUpdateRoleRequestValidator : AbstractValidator<CreateOrUpdateRoleRequest>
{
    public CreateOrUpdateRoleRequestValidator()
    {
        RuleFor(p => p.Name)
            .NotNull().NotEmpty().WithMessage("You must enter Name")
            .MaximumLength(13).WithMessage("Name cannot be longer than 13 characters");

        RuleFor(p => p.Description)
            .NotNull().NotEmpty().WithMessage("You must enter Description")
            .MaximumLength(100).WithMessage("Description cannot be longer than 100 characters");
    }
}
