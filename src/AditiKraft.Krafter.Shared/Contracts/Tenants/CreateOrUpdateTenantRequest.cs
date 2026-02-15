using FluentValidation;

namespace AditiKraft.Krafter.Shared.Contracts.Tenants;

public class CreateOrUpdateTenantRequest
{
    public string? Id { get; set; }
    public string? Identifier { get; set; }
    public string? Name { get; set; }
    public string AdminEmail { get; set; } = default!;
    public bool? IsActive { get; set; }
    public DateTime? ValidUpto { get; set; }
    public string? TablesToCopy { get; set; }
}

public class CreateOrUpdateTenantRequestValidator : AbstractValidator<CreateOrUpdateTenantRequest>
{
    public CreateOrUpdateTenantRequestValidator()
    {
        RuleFor(p => p.Name)
            .NotNull().NotEmpty().WithMessage("You must enter Name")
            .MaximumLength(40).WithMessage("Name cannot be longer than 40 characters");

        RuleFor(p => p.AdminEmail)
            .NotEmpty().WithMessage("Admin email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(p => p.Identifier)
            .NotEmpty().WithMessage("Identifier is required")
            .MaximumLength(10).WithMessage("Identifier cannot be longer than 10 characters");

        RuleFor(p => p.IsActive)
            .NotNull().WithMessage("IsActive is required");

        RuleFor(p => p.ValidUpto)
            .NotNull().WithMessage("ValidUpto is required");
    }
}
