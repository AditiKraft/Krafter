using FluentValidation;
using Krafter.Shared.Common;

namespace Krafter.Shared.Features.Tenants;

public sealed class CreateOrUpdate
{
    public sealed class CreateOrUpdateTenantRequestInput
    {
        public string? Id { get; set; }
        public string? Identifier { get; set; }
        public string? Name { get; set; }
        public string AdminEmail { get; set; } = default!;
        public bool? IsActive { get; set; }
        public DateTime? ValidUpto { get; set; }
    }


    internal sealed class Validator : AbstractValidator<CreateOrUpdateTenantRequestInput>
    {
        public Validator()
        {
            RuleFor(p => p.Name)
                .NotNull().NotEmpty().WithMessage("You must enter Name")
                .MaximumLength(40)
                ;

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
                .NotNull()
                ;
        }
    }
}
