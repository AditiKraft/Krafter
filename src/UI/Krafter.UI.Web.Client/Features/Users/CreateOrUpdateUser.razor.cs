using Krafter.Api.Client;
using Krafter.Api.Client.Models;
using Mapster;

namespace Krafter.UI.Web.Client.Features.Users;

public partial class CreateOrUpdateUser(
    DialogService dialogService,
    KrafterClient krafterClient
) : ComponentBase
{
    [Parameter] public UserDto? UserInput { get; set; } = new();

    private CreateUserRequest CreateUserRequest = new();
    private CreateUserRequest OriginalCreateUserRequest = new();
    public UserRoleDtoListResponse? UserRoles { get; set; }
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
                UserRoles = await krafterClient.Users.GetRoles[UserInput.Id].GetAsync();
                CreateUserRequest.Roles = UserRoles?
                    .Data?
                    .Where(c => !string.IsNullOrEmpty(c.RoleId))
                    .Select(c => c.RoleId!).ToList();
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

            Response? result = await krafterClient.Users.CreateOrUpdate.PostAsync(finalInput);
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
