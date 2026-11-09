using System.Reflection;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using AditiKraft.Krafter.Tests.UI;
using AditiKraft.Krafter.UI.Web.Client.Features.Roles.Common;
using AditiKraft.Krafter.UI.Web.Client.Features.Users;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Radzen;
using Radzen.Blazor;

namespace AditiKraft.Krafter.Tests.Users;

public sealed class UserEditorTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("error")]
    [InlineData("null-data")]
    [InlineData("network-error")]
    public async Task FailedMembershipLoadBlocksIncompleteReplacementAndRetryKeepsExistingRoles(string failure)
    {
        await using var editor = new EditorTestContext();
        editor.Users.Load = () => failure switch
        {
            "error" => Task.FromResult(new Response<List<UserRoleDto>> { IsError = true, StatusCode = 403 }),
            "null-data" => Task.FromResult(new Response<List<UserRoleDto>>()),
            _ => throw new HttpRequestException("Controlled lookup failure")
        };
        HtmlRootComponent root = await RenderAsync(editor);
        await root.QuiescenceTask.WaitAsync(Deadline);

        await editor.RunAsync(async () =>
        {
            await InvokeNativeRoleAfterRenderAsync(editor, firstRender: true);
            Assert.True(editor.RoleSelector.Disabled);
            Assert.True(editor.Button("Save").Disabled);
            Assert.Contains("Could not load user roles", root.ToHtmlString());
            editor.UserForm.Data.FirstName = "Edited profile";
            // Even a queued child callback cannot replace the roles before their baseline loads.
            await editor.RoleSelection.ValueChanged.InvokeAsync(["added-c"]);
            Assert.Empty(editor.UserForm.Data.Roles);
            await editor.UserForm.Submit.InvokeAsync(editor.UserForm.Data);
            Assert.Empty(editor.Users.Updates);

            editor.Users.Load = () => Task.FromResult(ControlledUsersApi.Memberships());
            await editor.Button("Retry").Click.InvokeAsync(new MouseEventArgs());
            Assert.Equal(new[] { "existing-a", "existing-b" }, editor.UserForm.Data.Roles);
            Assert.Equal("Edited profile", editor.UserForm.Data.FirstName);
            Assert.False(editor.RoleSelector.Disabled);
            Assert.False(editor.Button("Save").Disabled);
            Assert.DoesNotContain("Could not load user roles", root.ToHtmlString());
            await editor.RoleSelection.ValueChanged.InvokeAsync([.. editor.UserForm.Data.Roles, "added-c"]);
            await editor.UserForm.Submit.InvokeAsync(editor.UserForm.Data);
        });

        CreateUserRequest saved = Assert.Single(editor.Users.Updates);
        Assert.Equal(new[] { "existing-a", "existing-b", "added-c" }, saved.Roles);
        Assert.Equal("Edited profile", saved.FirstName);
        Assert.Equal(2, editor.Users.LoadCount);
    }

    [Fact]
    public async Task DeferredMembershipLoadCannotBeEditedOrSubmittedBeforeItCompletes()
    {
        await using var editor = new EditorTestContext();
        var completion = new TaskCompletionSource<Response<List<UserRoleDto>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        editor.Users.Load = () => completion.Task;
        HtmlRootComponent root = await RenderAsync(editor);
        try
        {
            await editor.RunAsync(async () =>
            {
                Assert.True(editor.RoleSelector.Disabled);
                Assert.True(editor.Button("Save").Disabled);
                Assert.Contains("Loading user roles", root.ToHtmlString());
                await editor.RoleSelection.ValueChanged.InvokeAsync(["added-c"]);
                await editor.UserForm.Submit.InvokeAsync(editor.UserForm.Data);
                Assert.Empty(editor.Users.Updates);
                Assert.Empty(editor.UserForm.Data.Roles);
            });
        }
        finally
        {
            completion.TrySetResult(ControlledUsersApi.Memberships());
        }
        await root.QuiescenceTask.WaitAsync(Deadline);
        await editor.RunAsync(() => Assert.Equal(new[] { "existing-a", "existing-b" }, editor.UserForm.Data.Roles));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ProfileOnlyEditorCannotSubmitMembershipChanges(bool canView, bool canUpdate)
    {
        string[] permissions =
        [
            .. canView ? new[] { "Permissions.UserRoles.View" } : [],
            .. canUpdate ? new[] { "Permissions.UserRoles.Update" } : []
        ];
        await using var editor = new EditorTestContext(permissions);
        HtmlRootComponent root = await RenderAsync(editor);
        await root.QuiescenceTask.WaitAsync(Deadline);
        await editor.RunAsync(async () =>
        {
            Assert.Empty(editor.All.OfType<MultiSelectRoleDropDownDataGrid>());
            Assert.False(editor.Button("Save").Disabled);
            Assert.Equal(canView, root.ToHtmlString().Contains("ExistingA", StringComparison.Ordinal));
            editor.UserForm.Data.FirstName = "Profile only";
            editor.UserForm.Data.Roles = ["added-c"];
            await editor.UserForm.Submit.InvokeAsync(editor.UserForm.Data);
        });
        CreateUserRequest saved = Assert.Single(editor.Users.Updates);
        Assert.Empty(saved.Roles);
        Assert.Equal("Profile only", saved.FirstName);
        Assert.Equal(canView ? 1 : 0, editor.Users.LoadCount);
    }

    [Fact]
    public async Task ViewOnlyMembershipFailureStillAllowsProfileUpdateWithoutRoles()
    {
        await using var editor = new EditorTestContext(["Permissions.UserRoles.View"]);
        editor.Users.Load = () => Task.FromResult(new Response<List<UserRoleDto>> { IsError = true, StatusCode = 503 });
        HtmlRootComponent root = await RenderAsync(editor);
        await root.QuiescenceTask.WaitAsync(Deadline);
        await editor.RunAsync(async () =>
        {
            Assert.Contains("Could not load user roles", root.ToHtmlString());
            Assert.False(editor.Button("Save").Disabled);
            await editor.UserForm.Submit.InvokeAsync(editor.UserForm.Data);
        });
        Assert.Empty(Assert.Single(editor.Users.Updates).Roles);
    }

    [Fact]
    public async Task NativeRoleSelectorClearUpdatesParentAndCannotBeMisreportedAsSaved()
    {
        await using var editor = new EditorTestContext();
        HtmlRootComponent root = await RenderAsync(editor);
        await root.QuiescenceTask.WaitAsync(Deadline);
        await editor.RunAsync(async () =>
        {
            await InvokeNativeRoleAfterRenderAsync(editor, firstRender: true);
            // Exercise the pinned Radzen component's native Clear method, which first
            // clears its own value and then emits Change(null) through our child handler.
            MethodInfo? clear = typeof(RadzenDropDownDataGrid<IEnumerable<string>>).GetMethod("Clear", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(clear);
            await Assert.IsAssignableFrom<Task>(clear.Invoke(editor.RoleSelector, null));
            Assert.Empty(editor.UserForm.Data.Roles);
            Assert.Contains("This editor cannot remove all roles", root.ToHtmlString());
            Assert.True(editor.Button("Save").Disabled);
            await editor.UserForm.Submit.InvokeAsync(editor.UserForm.Data);
            Assert.Empty(editor.Users.Updates);
            await editor.RoleSelector.Change.InvokeAsync(new[] { "existing-b", "existing-a" });
            await editor.UserForm.Submit.InvokeAsync(editor.UserForm.Data);
        });
        Assert.Empty(Assert.Single(editor.Users.Updates).Roles);
    }

    [Fact]
    public async Task NativeFirstRenderLoadsTheRolePageAndBlocksPrematureClear()
    {
        await using var editor = new EditorTestContext();
        SetPagedMemberships(editor);
        HtmlRootComponent root = await RenderAsync(editor);
        await root.QuiescenceTask.WaitAsync(Deadline);

        await editor.RunAsync(async () =>
        {
            Assert.Null(editor.RoleSelector.Data);
            Assert.True(editor.RoleSelector.Disabled);
            Assert.Equal(0, editor.Roles.LoadCount);
            MethodInfo? clear = typeof(RadzenDropDownDataGrid<IEnumerable<string>>)
                .GetMethod("Clear", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(clear);
            await Assert.IsAssignableFrom<Task>(clear.Invoke(editor.RoleSelector, null));
            Assert.Equal(new[] { "existing-off-page" }, editor.UserForm.Data.Roles);

            // HtmlRenderer omits after-render callbacks. Run the actual pinned
            // native lifecycle method; do not invoke LoadData ourselves here.
            await InvokeNativeRoleAfterRenderAsync(editor, firstRender: true);
            Assert.Equal(1, editor.Roles.LoadCount);
            Assert.Equal(10, Assert.IsAssignableFrom<IEnumerable<RoleDto>>(editor.RoleSelector.Data).Count());
            Assert.Equal(11, editor.RoleSelector.Count);
            Assert.False(editor.RoleSelector.Disabled);
            await InvokeNativeRoleAfterRenderAsync(editor, firstRender: false);
            Assert.Equal(1, editor.Roles.LoadCount);
        });
    }

    [Theory]
    [InlineData("error")]
    [InlineData("null-data")]
    [InlineData("network-error")]
    public async Task FailedInitialRoleCatalogLoadKeepsNativeRemovalSafe(string failure)
    {
        await using var editor = new EditorTestContext();
        editor.Roles.Load = _ => failure switch
        {
            "error" => Task.FromResult(new Response<PaginationResponse<RoleDto>> { IsError = true, StatusCode = 503 }),
            "null-data" => Task.FromResult(new Response<PaginationResponse<RoleDto>>()),
            _ => throw new HttpRequestException("Controlled catalog failure")
        };
        HtmlRootComponent root = await RenderAsync(editor);
        await root.QuiescenceTask.WaitAsync(Deadline);

        await editor.RunAsync(async () =>
        {
            await InvokeNativeRoleAfterRenderAsync(editor, firstRender: true);
            Assert.Equal(1, editor.Roles.LoadCount);
            Assert.Empty(editor.RoleSelector.Data.Cast<RoleDto>());
            Assert.False(editor.RoleSelector.Disabled);

            RoleDto chip = editor.RoleSelection.SelectionData.Single(role => role.Id == "existing-a");
            MethodInfo? remove = typeof(RadzenDropDownDataGrid<IEnumerable<string>>)
                .GetMethod("OnChipRemove", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(remove);
            await Assert.IsAssignableFrom<Task>(remove.Invoke(editor.RoleSelector, [chip]));
            Assert.Equal(new[] { "existing-b" }, editor.UserForm.Data.Roles);

            MethodInfo? clear = typeof(RadzenDropDownDataGrid<IEnumerable<string>>)
                .GetMethod("Clear", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(clear);
            await Assert.IsAssignableFrom<Task>(clear.Invoke(editor.RoleSelector, null));
            Assert.Empty(editor.UserForm.Data.Roles);
            Assert.True(editor.Button("Save").Disabled);
            await editor.UserForm.Submit.InvokeAsync(editor.UserForm.Data);
            Assert.Empty(editor.Users.Updates);
        });
    }

    [Fact]
    public async Task NativeRoleSelectionPreservesMembershipOutsideTheLoadedPage()
    {
        await using var editor = new EditorTestContext();
        SetPagedMemberships(editor);
        HtmlRootComponent root = await RenderAsync(editor);
        await root.QuiescenceTask.WaitAsync(Deadline);

        await editor.RunAsync(async () =>
        {
            // Static rendering does not run the browser's initial LoadData callback.
            // Invoke that real callback so production paging supplies Radzen's data.
            await editor.RoleSelector.LoadData.InvokeAsync(new LoadDataArgs { Skip = 0, Top = 10 });
            RoleDto[] visible = editor.RoleSelector.Data.Cast<RoleDto>().ToArray();
            Assert.Equal(10, visible.Length);
            Assert.Equal(11, editor.RoleSelector.Count);
            Assert.DoesNotContain(visible, role => role.Id == "existing-off-page");
            Assert.Equal(new[] { "existing-off-page" }, editor.UserForm.Data.Roles);
            Assert.Contains("OffPage", root.ToHtmlString());

            // Exercise the same native method as the checkbox/row selection. Do not
            // manufacture the replacement list through Change or ValueChanged.
            await editor.RoleSelector.SelectItem(visible.Single(role => role.Id == "visible-01"));
            Assert.Equal(new[] { "existing-off-page", "visible-01" },
                editor.UserForm.Data.Roles.Order(StringComparer.Ordinal));
            await editor.UserForm.Submit.InvokeAsync(editor.UserForm.Data);
        });

        Assert.Equal(new[] { "existing-off-page", "visible-01" },
            Assert.Single(editor.Users.Updates).Roles.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeRoleDeselectionPreservesOtherPagesAndRemovesTheChosenMembership(bool removeOffPageChip)
    {
        await using var editor = new EditorTestContext();
        SetPagedMemberships(editor);
        HtmlRootComponent root = await RenderAsync(editor);
        await root.QuiescenceTask.WaitAsync(Deadline);

        await editor.RunAsync(async () =>
        {
            await editor.RoleSelector.LoadData.InvokeAsync(new LoadDataArgs { Skip = 0, Top = 10 });
            await editor.RoleSelector.SelectItem(editor.RoleSelector.Data.Cast<RoleDto>()
                .Single(role => role.Id == "visible-01"));
            Assert.Equal(new[] { "existing-off-page", "visible-01" },
                editor.UserForm.Data.Roles.Order(StringComparer.Ordinal));

            if (removeOffPageChip)
            {
                // This is the native chip-removal callback, while the selected role
                // remains outside Data. A simple union with off-page IDs would fail.
                Assert.DoesNotContain(editor.RoleSelector.Data.Cast<RoleDto>(), role => role.Id == "existing-off-page");
                RoleDto chip = editor.RoleSelection.SelectionData.Single(role => role.Id == "existing-off-page");
                MethodInfo? remove = typeof(RadzenDropDownDataGrid<IEnumerable<string>>)
                    .GetMethod("OnChipRemove", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(remove);
                await Assert.IsAssignableFrom<Task>(remove.Invoke(editor.RoleSelector, [chip]));
            }
            else
            {
                await editor.RoleSelector.LoadData.InvokeAsync(new LoadDataArgs { Skip = 10, Top = 10 });
                RoleDto membership = Assert.Single(editor.RoleSelector.Data.Cast<RoleDto>());
                Assert.Equal("existing-off-page", membership.Id);
                await editor.RoleSelector.SelectItem(membership);
            }

            Assert.Equal(new[] { "visible-01" }, editor.UserForm.Data.Roles);
            if (removeOffPageChip) Assert.DoesNotContain("OffPage", root.ToHtmlString());
            await editor.UserForm.Submit.InvokeAsync(editor.UserForm.Data);
        });

        Assert.Equal(new[] { "visible-01" }, Assert.Single(editor.Users.Updates).Roles);
    }

    [Fact]
    public async Task NativeRoleClearRemovesSelectionsAcrossPagesAndKeepsEmptySelectionGuard()
    {
        await using var editor = new EditorTestContext();
        SetPagedMemberships(editor);
        HtmlRootComponent root = await RenderAsync(editor);
        await root.QuiescenceTask.WaitAsync(Deadline);

        await editor.RunAsync(async () =>
        {
            await editor.RoleSelector.LoadData.InvokeAsync(new LoadDataArgs { Skip = 0, Top = 10 });
            await editor.RoleSelector.SelectItem(editor.RoleSelector.Data.Cast<RoleDto>()
                .Single(role => role.Id == "visible-01"));
            Assert.Equal(2, editor.UserForm.Data.Roles.Count);
            MethodInfo? clear = typeof(RadzenDropDownDataGrid<IEnumerable<string>>)
                .GetMethod("Clear", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(clear);
            await Assert.IsAssignableFrom<Task>(clear.Invoke(editor.RoleSelector, null));

            Assert.Empty(editor.UserForm.Data.Roles);
            Assert.Contains("This editor cannot remove all roles", root.ToHtmlString());
            Assert.True(editor.Button("Save").Disabled);
            await editor.UserForm.Submit.InvokeAsync(editor.UserForm.Data);
        });

        Assert.Empty(editor.Users.Updates);
    }

    private static void SetPagedMemberships(EditorTestContext editor)
    {
        editor.Users.Load = () => Task.FromResult(Response<List<UserRoleDto>>.Success(
        [
            new UserRoleDto { RoleId = "existing-off-page", RoleName = "OffPage", Enabled = true }
        ]));
        editor.Roles.Items = Enumerable.Range(1, 10)
            .Select(index => new RoleDto { Id = $"visible-{index:00}", Name = $"Visible{index:00}" })
            .Append(new RoleDto { Id = "existing-off-page", Name = "OffPage" }).ToList();
    }

    private static Task InvokeNativeRoleAfterRenderAsync(EditorTestContext editor, bool firstRender)
    {
        MethodInfo? afterRender = typeof(RadzenDropDownDataGrid<IEnumerable<string>>)
            .GetMethod("OnAfterRenderAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(afterRender);
        return Assert.IsAssignableFrom<Task>(afterRender.Invoke(editor.RoleSelector, [firstRender]));
    }

    private static Task<HtmlRootComponent> RenderAsync(EditorTestContext editor) => editor.BeginAsync<CreateOrUpdateUser>(
        new Dictionary<string, object?>
        {
            [nameof(CreateOrUpdateUser.UserInput)] = new UserDto
            {
                Id = "existing-user", FirstName = "Existing", LastName = "User", Email = "user@example.test", IsActive = true
            }
        });
}
