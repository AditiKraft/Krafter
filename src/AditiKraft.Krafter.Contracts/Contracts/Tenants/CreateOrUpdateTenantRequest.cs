using AditiKraft.Krafter.Contracts.Common;
using FluentValidation;

namespace AditiKraft.Krafter.Contracts.Contracts.Tenants;

public class CreateOrUpdateTenantRequest
{
    public string? Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string AdminEmail { get; set; } = default!;
    public bool? IsActive { get; set; }
    public DateTime? ValidUpto { get; set; }
    public string? TablesToCopy { get; set; }
}

public class CreateOrUpdateTenantRequestValidator : AbstractValidator<CreateOrUpdateTenantRequest>
{
    public CreateOrUpdateTenantRequestValidator(AppUrls? urls = null)
    {
        urls ??= new AppUrls();
        RuleFor(p => p.Name)
            .NotNull().NotEmpty().WithMessage("You must enter Name")
            .MaximumLength(40).WithMessage("Name cannot be longer than 40 characters");

        RuleFor(p => p.AdminEmail)
            .NotEmpty().WithMessage("Admin email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(p => p.Identifier)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Identifier is required")
            .MaximumLength(10).WithMessage("Identifier cannot be longer than 10 characters")
            .Matches(@"\A[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\z")
            .WithMessage("Use lowercase letters, numbers, and hyphens. Start and end with a letter or number.")
            .Must((request, identifier) => !urls.IsReservedTenantIdentifier(identifier)
                || (request.Id == DefaultTenantConstants.Identifier && identifier == DefaultTenantConstants.Identifier))
            .WithMessage("This identifier is reserved. Choose a different identifier.");

        RuleFor(p => p.IsActive)
            .NotNull().WithMessage("IsActive is required");

        RuleFor(p => p.ValidUpto)
            .NotNull().WithMessage("ValidUpto is required");
    }
}
