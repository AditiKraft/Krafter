using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Users;
using Krafter.UI.Web.Client.Infrastructure.Refit;
using Mapster;

namespace Krafter.UI.Web.Client.Features.Users;

public partial class CreateOrUpdateUser(
    DialogService dialogService,
    IUsersApi usersApi
) : ComponentBase
{
    [Parameter] public UserDto? UserInput { get; set; } = new();

    private CreateUserRequest CreateUserRequest = new();
    private CreateUserRequest OriginalCreateUserRequest = new();
    public Response<List<UserRoleDto>>? UserRoles { get; set; }
    private bool isBusy = false;

    protected override async Task OnInitializedAsync()
    {
        if (UserInput is not null)
        {
            CreateUserRequest = UserInput.Adapt<CreateUserRequest>();
            OriginalCreateUserRequest = UserInput.Adapt<CreateUserRequest>();
            if (CreateUserRequest.Roles is null)
            {
                CreateUserRequest.Roles = new List<string>();
                OriginalCreateUserRequest.Roles = new List<string>();
            }

            if (!string.IsNullOrWhiteSpace(UserInput.Id))
            {
                UserRoles = await usersApi.GetUserRolesAsync(UserInput.Id);
                CreateUserRequest.Roles = UserRoles?
                    .Data?
                    .Where(c => !string.IsNullOrEmpty(c.RoleId))
                    .Select(c => c.RoleId!).ToList() ?? new List<string>();
                OriginalCreateUserRequest.Roles = CreateUserRequest.Roles;
            }
        }
    }

    private async void Submit(CreateUserRequest input)
    {
        if (UserInput is not null)
        {
            isBusy = true;
            CreateUserRequest finalInput = new();
            if (string.IsNullOrWhiteSpace(input.Id))
            {
                finalInput = input;
            }
            else
            {
                finalInput.Id = input.Id;
                if (input.Email != OriginalCreateUserRequest.Email)
                {
                    finalInput.Email = input.Email;
                }

                if (input.FirstName != OriginalCreateUserRequest.FirstName)
                {
                    finalInput.FirstName = input.FirstName;
                }

                if (input.LastName != OriginalCreateUserRequest.LastName)
                {
                    finalInput.LastName = input.LastName;
                }

                if (input.PhoneNumber != OriginalCreateUserRequest.PhoneNumber)
                {
                    finalInput.PhoneNumber = input.PhoneNumber;
                }

                if (input.UserName != OriginalCreateUserRequest.UserName)
                {
                    finalInput.UserName = input.UserName;
                }

                if (!input.Roles.ToHashSet().SetEquals(OriginalCreateUserRequest.Roles))
                {
                    finalInput.Roles = input.Roles;
                }

                finalInput.UpdateTenantEmail = true;
            }

            Response? result = await usersApi.CreateOrUpdateUserAsync(finalInput);
            isBusy = false;
            StateHasChanged();
            if (result is not null && result.IsError == false)
            {
                dialogService.Close(true);
            }
        }
        else
        {
            dialogService.Close(false);
        }
    }

    private void Cancel() => dialogService.Close();
}
