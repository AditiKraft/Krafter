using System.Reflection;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using AditiKraft.Krafter.Tests.UI;
using AditiKraft.Krafter.UI.Web.Client.Features.Roles.Common;
using AditiKraft.Krafter.UI.Web.Client.Features.Users;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;

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
            // Exercise the pinned Radzen component's native Clear method, which first
            // clears its own value and then emits Change(null) through our child handler.
            MethodInfo? clear = editor.RoleSelector.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.NonPublic);
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

    private static Task<HtmlRootComponent> RenderAsync(EditorTestContext editor) => editor.BeginAsync<CreateOrUpdateUser>(
        new Dictionary<string, object?>
        {
            [nameof(CreateOrUpdateUser.UserInput)] = new UserDto
            {
                Id = "existing-user", FirstName = "Existing", LastName = "User", Email = "user@example.test", IsActive = true
            }
        });
}
