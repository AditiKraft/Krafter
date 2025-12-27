using FluentValidation;

namespace Krafter.Shared.Features.Roles;

public sealed class UpdateRolePermissions
{
    public sealed class UpdateRolePermissionsRequest
    {
        public string RoleId { get; set; } = default!;
        public List<string> Permissions { get; set; } = [];
    }


    internal sealed class Validator : AbstractValidator<UpdateRolePermissionsRequest>
    {
        public Validator()
        {
            RuleFor(p => p.RoleId)
                .NotEmpty().WithMessage("Role ID is required");

            RuleFor(p => p.Permissions)
                .NotNull().WithMessage("Permissions list cannot be null");
        }
    }
}
