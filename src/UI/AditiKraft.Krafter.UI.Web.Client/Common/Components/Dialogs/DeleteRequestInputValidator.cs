using AditiKraft.Krafter.Shared.Common.Models;
using FluentValidation;

namespace AditiKraft.Krafter.UI.Web.Client.Common.Components.Dialogs;

public class DeleteRequestInputValidator : AbstractValidator<DeleteRequestInput>
{
    public DeleteRequestInputValidator()
    {
        RuleFor(x => x.DeleteReason)
            .NotEmpty().WithMessage("Delete Reason is required")
            .Length(5, 500).WithMessage("Delete Reason must be between 5 and 500 characters");
    }
}
