using FluentValidation;

namespace Krafter.Shared.Contracts.Roles;

/// <summary>
/// Request model for updating role permissions.
/// </summary>
public class UpdateRolePermissionsRequest
{
    public string RoleId { get; set; } = default!;
    public List<string> Permissions { get; set; } = [];
}

public class UpdateRolePermissionsRequestValidator : AbstractValidator<UpdateRolePermissionsRequest>
{
    public UpdateRolePermissionsRequestValidator()
    {
        RuleFor(p => p.RoleId)
            .NotEmpty().WithMessage("Role ID is required");

        RuleFor(p => p.Permissions)
            .NotNull().WithMessage("Permissions list cannot be null");
    }
}
