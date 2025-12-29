using Krafter.Shared.Common.Auth.Permissions;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Roles;
using Krafter.UI.Web.Client.Infrastructure.Refit;
using Krafter.UI.Web.Client.Infrastructure.Services;
using Mapster;

namespace Krafter.UI.Web.Client.Features.Roles;

public partial class CreateOrUpdateRole(
    DialogService dialogService,
    ApiCallService api,
    IRolesApi rolesApi) : ComponentBase
{
    private IEnumerable<string> selectedStandards = default!;

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

    [Parameter] public RoleDto? UserDetails { get; set; } = new();
    private CreateOrUpdateRoleRequest CreateUserRequest = new();
    private CreateOrUpdateRoleRequest OriginalCreateUserRequest = new();

    public List<KrafterPermission> AllRoles { get; set; } = default!;

    private IEnumerable<GroupPermissionData> GroupedData = new List<GroupPermissionData>();

    private bool isBusy = false;

    protected override async Task OnInitializedAsync()
    {
        if (UserDetails is not null)
        {
            CreateUserRequest = UserDetails.Adapt<CreateOrUpdateRoleRequest>();
            OriginalCreateUserRequest = UserDetails.Adapt<CreateOrUpdateRoleRequest>();
            if (!string.IsNullOrWhiteSpace(UserDetails.Id))
            {
                GroupedData = KrafterPermissions.All.GroupBy(c => c.Resource)
                    .SelectMany(i => new GroupPermissionData[] { new() { Resource = i.Key } }
                        .Concat(i.Select(o =>
                            new GroupPermissionData
                            {
                                Description = o.Description,
                                Action = o.Action,
                                IsBasic = o.IsBasic,
                                IsRoot = o.IsRoot,
                                FinalPermission = KrafterPermission.NameFor(o.Action, o.Resource)
                            }))).ToList();
                Response<RoleDto> rolePermissions = await api.CallAsync(
                    () => rolesApi.GetRolePermissionsAsync(UserDetails.Id),
                    showErrorNotification: true);
                CreateUserRequest.Permissions = rolePermissions?.Data?.Permissions ?? new List<string>();
                OriginalCreateUserRequest.Permissions = CreateUserRequest.Permissions;
            }
        }
    }

    private async void Submit(CreateOrUpdateRoleRequest input)
    {
        if (UserDetails is not null)
        {
            isBusy = true;
            Response result = await api.CallAsync(
                () => rolesApi.CreateOrUpdateRoleAsync(input),
                successMessage: "Role saved successfully");
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

    private void Cancel() => dialogService.Close(false);
}
