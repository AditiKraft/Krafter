using Krafter.Shared.Common.Auth.Permissions;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Roles;
using Krafter.UI.Web.Client.Infrastructure.Refit;
using Mapster;

namespace Krafter.UI.Web.Client.Features.Roles;

public partial class CreateOrUpdateRole(
    DialogService dialogService,
    IRolesApi rolesApi,
    NotificationService notificationService) : ComponentBase
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

    private bool KeyPosition;
    private bool ProcessOwner;

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
                Response<List<string>>? rolePermissions = await rolesApi.GetRolePermissionsAsync(UserDetails.Id);
                CreateUserRequest.Permissions = rolePermissions?.Data ?? new List<string>();
                OriginalCreateUserRequest.Permissions = CreateUserRequest.Permissions;
            }
        }
    }

    private async void Submit(CreateOrUpdateRoleRequest input)
    {
        if (UserDetails is not null)
        {
            isBusy = true;
            CreateOrUpdateRoleRequest finalInput = new();
            if (string.IsNullOrWhiteSpace(input.Id))
            {
                finalInput = input;
            }
            else
            {
                finalInput.Id = input.Id;
                if (input.Name != OriginalCreateUserRequest.Name)
                {
                    finalInput.Name = input.Name;
                }

                if (input.Description != OriginalCreateUserRequest.Description)
                {
                    finalInput.Description = input.Description;
                }


                if (!input.Permissions.ToHashSet().SetEquals(OriginalCreateUserRequest.Permissions))
                {
                    finalInput.Permissions = input.Permissions;
                }
            }

            Response? result = await rolesApi.CreateOrUpdateRoleAsync(finalInput);
            isBusy = false;
            StateHasChanged();
            if (result is null || result is { IsError: true })
            {
                if (string.IsNullOrWhiteSpace(input.Id))
                {
                    await dialogService.Alert(
                        "If you need to add a backup user and person in charge to this role, first add this role to the user by updating the user. Then come back here again and click on update to choose the backup person and person in charge.",
                        "Information", new AlertOptions { OkButtonText = "OK", Top = "5vh" });
                }

                dialogService.Close(true);
            }
        }
        else
        {
            dialogService.Close(false);
        }
    }

    private void OnChange(object? value)
    {
        if (value is null)
        {
            //CreateUserRequest.BackupUserId = "";
        }
    }

    private void Cancel() => dialogService.Close(false);
}
