using AditiKraft.Krafter.UI.Web.Client.Common.Components.Dialogs;
using AditiKraft.Krafter.UI.Web.Client.Common.Permissions;
using Mapster;
using Microsoft.AspNetCore.Authorization;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Users;

public partial class CreateOrUpdateUser(
    DialogService dialogService,
    ApiCallService api,
    IUsersApi usersApi,
    AuthenticationStateProvider authenticationStateProvider,
    IAuthorizationService authorization) : EditorDialogBase(dialogService)
{
    [Parameter] public UserDto UserInput { get; set; } = new();

    private CreateUserRequest CreateUserRequest = new();
    private List<UserRoleDto> userRoles = [];
    private HashSet<string> originalRoles = [];
    private bool permissionsChecked;
    private bool canViewRoles;
    private bool canUpdateRoles;
    private bool rolesLoaded;
    private bool isLoadingRoles;
    private bool roleLoadFailed;

    private bool IsExisting => !string.IsNullOrWhiteSpace(UserInput.Id);
    private bool CanEditRoles => !IsDisposed && !IsSaving && canUpdateRoles && rolesLoaded;
    private bool EmptyRoleSelection => canUpdateRoles && rolesLoaded &&
        originalRoles.Count > 0 && CreateUserRequest.Roles.Count == 0;
    private bool CanSave => !IsDisposed && !IsSaving && permissionsChecked &&
        (!IsExisting || !canUpdateRoles || rolesLoaded) && !EmptyRoleSelection;

    protected override async Task OnInitializedAsync()
    {
        CreateUserRequest = UserInput.Adapt<CreateUserRequest>();
        CreateUserRequest.Roles = [];
        AuthenticationState state = await authenticationStateProvider.GetAuthenticationStateAsync();
        canViewRoles = (await authorization.AuthorizeAsync(state.User,
            UiPermissionPolicy.NameFor(PermissionAction.View, PermissionResource.UserRoles))).Succeeded;
        canUpdateRoles = canViewRoles && (await authorization.AuthorizeAsync(state.User,
            UiPermissionPolicy.NameFor(PermissionAction.Update, PermissionResource.UserRoles))).Succeeded;
        if (IsDisposed) return;

        permissionsChecked = true;
        if (IsExisting && canViewRoles)
        {
            await LoadRolesAsync();
        }
    }

    private async Task LoadRolesAsync()
    {
        if (IsDisposed || IsSaving || isLoadingRoles || !canViewRoles) return;

        rolesLoaded = false;
        roleLoadFailed = false;
        isLoadingRoles = true;
        try
        {
            Response<List<UserRoleDto>> response = await api.CallAsync(
                () => usersApi.GetUserRolesAsync(UserInput.Id), showErrorNotification: true);
            if (IsDisposed) return;
            if (response is not { IsError: false, Data: not null })
            {
                roleLoadFailed = true;
                return;
            }

            userRoles = response.Data;
            CreateUserRequest.Roles = userRoles.Where(role => role.Enabled && !string.IsNullOrEmpty(role.RoleId))
                .Select(role => role.RoleId!).Distinct(StringComparer.Ordinal).ToList();
            originalRoles = new HashSet<string>(CreateUserRequest.Roles, StringComparer.Ordinal);
            rolesLoaded = true;
        }
        finally
        {
            if (!IsDisposed) isLoadingRoles = false;
        }
    }

    private void ChangeRoles(List<string> roles)
    {
        if (CanEditRoles)
        {
            CreateUserRequest.Roles = [.. roles];
        }
    }

    private async Task SubmitAsync(CreateUserRequest input)
    {
        if (!CanSave || !TryBeginSave()) return;

        // An empty roles payload deliberately preserves memberships on profile-only updates.
        // Copy the request so an in-flight mutation cannot observe later form edits.
        CreateUserRequest request = input.Adapt<CreateUserRequest>();
        request.Roles = canUpdateRoles && rolesLoaded && !originalRoles.SetEquals(input.Roles)
            ? [.. input.Roles] : [];
        try
        {
            Response result = string.IsNullOrWhiteSpace(request.Id)
                ? await api.CallAsync(() => usersApi.CreateUserAsync(request), successMessage: "User created successfully")
                : await api.CallAsync(() => usersApi.UpdateUserAsync(request.Id, request), successMessage: "User updated successfully");
            if (!result.IsError)
            {
                CloseAfterSave();
            }
        }
        finally
        {
            EndSave();
        }
    }
}
