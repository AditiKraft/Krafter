using Krafter.Api.Client;
using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client.Common.Permissions;
using Mapster;

namespace Krafter.UI.Web.Client.Features.Roles;

public partial class CreateOrUpdateRole(
    DialogService dialogService,
    KrafterClient krafterClient,
    NotificationService notificationService) : ComponentBase
{
    private IEnumerable<string> selectedStandards;

    public class GroupPermissionData
    {
        public string Description { get; set; }
        public string Action { get; set; }
        public string Resource { get; set; }
        public string FinalPermission { get; set; }
        public bool IsBasic { get; set; }
        public bool IsRoot { get; set; }

        public bool IsGroup => Resource != null;
    }

    [Parameter] public RoleDto? UserDetails { get; set; } = new();
    private CreateOrUpdateRoleRequest CreateUserRequest = new();
    private CreateOrUpdateRoleRequest OriginalCreateUserRequest = new();

    public List<KrafterPermission> AllRoles { get; set; }

    //  public List<string> SelectedRolesId { get; set; } = new List<string>();
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
                RoleDto? roleWithPermissions =
                    (await krafterClient.Roles.GetByIdWithPermissions[UserDetails.Id].GetAsync())?.Data;
                CreateUserRequest.Permissions = roleWithPermissions is { Permissions: not null }
                    ? roleWithPermissions.Permissions
                    : new List<string>();
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

            Response? result = await krafterClient.Roles.CreateOrUpdate.PostAsync(finalInput);
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
