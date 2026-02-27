using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using Mapster;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Users;

public partial class CreateOrUpdateUser(
    DialogService dialogService,
    ApiCallService api,
    IUsersApi usersApi
) : ComponentBase
{
    [Parameter] public UserDto UserInput { get; set; } = new();

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
                UserRoles = await api.CallAsync(() => usersApi.GetUserRolesAsync(UserInput.Id),
                    showErrorNotification: true);
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
            Response result;
            if (string.IsNullOrWhiteSpace(input.Id))
            {
                result = await api.CallAsync(
                    () => usersApi.CreateUserAsync(input),
                    successMessage: "User created successfully");
            }
            else
            {
                result = await api.CallAsync(
                    () => usersApi.UpdateUserAsync(input.Id, input),
                    successMessage: "User updated successfully");
            }
            isBusy = false;
            StateHasChanged();
            if (result is { IsError: false })
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
