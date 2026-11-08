using AditiKraft.Krafter.UI.Web.Client.Common.Components.Dialogs;
using AditiKraft.Krafter.UI.Web.Client.Common.Permissions;
using Mapster;
using Microsoft.AspNetCore.Authorization;

namespace AditiKraft.Krafter.UI.Web.Client.Features.Roles;

public partial class CreateOrUpdateRole(
    DialogService dialogService,
    ApiCallService api,
    IRolesApi rolesApi,
    AuthenticationStateProvider authenticationStateProvider,
    IAuthorizationService authorization) : EditorDialogBase(dialogService)
{
    public class GroupPermissionData
    {
        public string Description { get; set; } = default!;
        public string Action { get; set; } = default!;
        public string Resource { get; set; } = default!;
        public string FinalPermission { get; set; } = default!;
        public bool IsBasic { get; set; }
        public bool IsRoot { get; set; }
        public bool IsGroup => Resource != null;
    }

    [Parameter] public RoleDto UserDetails { get; set; } = new();
    private CreateOrUpdateRoleRequest CreateUserRequest = new();
    private HashSet<string> originalPermissions = [];
    private IEnumerable<GroupPermissionData> GroupedData = [];
    private bool permissionsChecked;
    private bool canViewPermissions;
    private bool canUpdatePermissions;
    private bool permissionsLoaded;
    private bool isLoadingPermissions;
    private bool permissionLoadFailed;

    private bool CanEditPermissions => !IsDisposed && !IsSaving && canUpdatePermissions && permissionsLoaded;
    private bool CanSave => !IsDisposed && !IsSaving && permissionsChecked &&
        (!canUpdatePermissions || permissionsLoaded);

    protected override async Task OnInitializedAsync()
    {
        CreateUserRequest = UserDetails.Adapt<CreateOrUpdateRoleRequest>();
        CreateUserRequest.Permissions = [];
        AuthenticationState state = await authenticationStateProvider.GetAuthenticationStateAsync();
        canViewPermissions = (await authorization.AuthorizeAsync(state.User,
            UiPermissionPolicy.NameFor(PermissionAction.View, PermissionResource.RoleClaims))).Succeeded;
        canUpdatePermissions = canViewPermissions && (await authorization.AuthorizeAsync(state.User,
            UiPermissionPolicy.NameFor(PermissionAction.Update, PermissionResource.RoleClaims))).Succeeded;
        if (IsDisposed) return;

        permissionsChecked = true;
        if (!canViewPermissions) return;

        GroupedData = PermissionCatalog.All.GroupBy(permission => permission.Resource)
            .SelectMany(group => new GroupPermissionData[] { new() { Resource = group.Key } }
                .Concat(group.Select(permission => new GroupPermissionData
                {
                    Description = permission.Description,
                    Action = permission.Action,
                    IsBasic = permission.IsBasic,
                    IsRoot = permission.IsRoot,
                    FinalPermission = PermissionDefinition.NameFor(permission.Action, permission.Resource)
                }))).ToList();
        if (string.IsNullOrWhiteSpace(UserDetails.Id))
        {
            permissionsLoaded = true;
        }
        else
        {
            await LoadPermissionsAsync();
        }
    }

    private async Task LoadPermissionsAsync()
    {
        if (IsDisposed || IsSaving || isLoadingPermissions || !canViewPermissions) return;

        permissionsLoaded = false;
        permissionLoadFailed = false;
        isLoadingPermissions = true;
        try
        {
            Response<RoleDto> response = await api.CallAsync(
                () => rolesApi.GetRolePermissionsAsync(UserDetails.Id), showErrorNotification: true);
            if (IsDisposed) return;
            if (response is not { IsError: false, Data.Permissions: not null })
            {
                permissionLoadFailed = true;
                return;
            }

            CreateUserRequest.Permissions = [.. response.Data.Permissions];
            originalPermissions = new HashSet<string>(response.Data.Permissions, StringComparer.Ordinal);
            permissionsLoaded = true;
        }
        finally
        {
            if (!IsDisposed) isLoadingPermissions = false;
        }
    }

    private void ChangePermissions(List<string>? permissions)
    {
        if (CanEditPermissions)
        {
            CreateUserRequest.Permissions = permissions is null ? [] : [.. permissions];
        }
    }

    private async Task SubmitAsync(CreateOrUpdateRoleRequest input)
    {
        if (!CanSave || !TryBeginSave()) return;

        CreateOrUpdateRoleRequest request = input.Adapt<CreateOrUpdateRoleRequest>();
        request.Permissions = canUpdatePermissions && permissionsLoaded &&
            !originalPermissions.SetEquals(input.Permissions ?? [])
                ? [.. (input.Permissions ?? [])] : null;
        try
        {
            Response result = string.IsNullOrWhiteSpace(request.Id)
                ? await api.CallAsync(() => rolesApi.CreateRoleAsync(request), successMessage: "Role created successfully")
                : await api.CallAsync(() => rolesApi.UpdateRoleAsync(request.Id, request), successMessage: "Role updated successfully");
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
