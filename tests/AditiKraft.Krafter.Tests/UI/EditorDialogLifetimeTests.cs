using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using AditiKraft.Krafter.UI.Web.Client.Common.Components.Dialogs;
using AditiKraft.Krafter.UI.Web.Client.Features.Roles;
using AditiKraft.Krafter.UI.Web.Client.Features.Tenants;
using AditiKraft.Krafter.UI.Web.Client.Features.Users;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Radzen.Blazor;

namespace AditiKraft.Krafter.Tests.UI;

public sealed class EditorDialogLifetimeTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("user")]
    [InlineData("role")]
    [InlineData("tenant")]
    public async Task PendingSaveCannotDismissAndForcedCloseCannotAffectNewerDialog(string kind)
    {
        await using var page = new EditorTestContext();
        var response = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);
        var saveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Response> Save()
        {
            saveEntered.TrySetResult();
            return response.Task;
        }
        page.Users.Save = (_, _) => Save();
        page.Roles.Save = (_, _) => Save();
        page.Tenants.Save = (_, _) => Save();
        var root = await page.RenderAsync<RadzenDialog>();
        Task<dynamic> firstDialog = null!;
        Task<dynamic> secondDialog = null!;
        Task firstSave = null!;
        EditorForm secondForm = null!;
        try
        {
            await page.RunAsync(() => { firstDialog = Open(page, kind, "first"); });
            await page.RunAsync(() =>
            {
                EditorForm form = Form(page, kind);
                form.Edit("First input");
                firstSave = form.Submit();
            });
            await saveEntered.Task.WaitAsync(Deadline);
            await page.RunAsync(async () =>
            {
                var editor = page.All.OfType<EditorDialogBase>().Last();
                Assert.False(editor.EditorDialog!.Options.ShowClose);
                Assert.False(editor.EditorDialog.Options.CloseDialogOnEsc);
                Assert.False(editor.EditorDialog.Options.CloseDialogOnOverlayClick);
                Assert.True(page.Button("Save").IsBusy);
                Assert.True(page.Button("Cancel").Disabled);
                Assert.Contains("<fieldset disabled", root.ToHtmlString());
                await page.Button("Cancel").Click.InvokeAsync(new MouseEventArgs());
                await Form(page, kind).Escape();
                await Form(page, kind).Submit();
                Assert.False(firstDialog.IsCompleted);
                Assert.Equal(1, MutationCount(page));

                // Navigation and other application code can still remove a dialog. Exercise
                // the actual Radzen close/disposal path, not a hand-written disposal flag.
                page.Dialogs.Close(false);
            });
            Assert.Equal(false, (object?)await firstDialog.WaitAsync(Deadline));
            Assert.False(firstSave.IsCompleted);

            await page.RunAsync(() => { secondDialog = Open(page, kind, "second"); });
            await page.RunAsync(() =>
            {
                secondForm = Form(page, kind);
                secondForm.Edit("Unsaved second input");
                Assert.False(secondDialog.IsCompleted);
            });
            response.TrySetResult(Response.Success());
            await firstSave.WaitAsync(Deadline);
            await page.RunAsync(async () =>
            {
                Assert.False(secondDialog.IsCompleted);
                Assert.Equal("Unsaved second input", secondForm.Value());
                Assert.Equal("Unsaved second input", Form(page, kind).Value());
                Assert.Equal(1, MutationCount(page));
                await page.Button("Cancel").Click.InvokeAsync(new MouseEventArgs());
            });
            Assert.Equal(false, (object?)await secondDialog.WaitAsync(Deadline));
        }
        finally
        {
            response.TrySetResult(Response.Success());
            if (firstSave is not null) await firstSave.WaitAsync(Deadline);
        }
    }

    [Theory]
    [InlineData("user")]
    [InlineData("role")]
    [InlineData("tenant")]
    public async Task SaveFailureKeepsInputAndAllowsEscapeWithoutReplayingSave(string kind)
    {
        await using var page = new EditorTestContext();
        static Task<Response> Failure() => Task.FromResult(new Response { IsError = true, StatusCode = 503 });
        page.Users.Save = (_, _) => Failure();
        page.Roles.Save = (_, _) => Failure();
        page.Tenants.Save = (_, _) => Failure();
        await page.RenderAsync<RadzenDialog>();
        Task<dynamic> dialog = null!;
        await page.RunAsync(() => { dialog = Open(page, kind, "retry"); });
        await page.RunAsync(async () =>
        {
            EditorForm form = Form(page, kind);
            form.Edit("Keep this input after failure");
            await form.Submit();
            Assert.False(dialog.IsCompleted);
            Assert.Equal("Keep this input after failure", form.Value());
            Assert.False(page.Button("Save").IsBusy);
            Assert.False(page.Button("Cancel").Disabled);
            await form.Escape();
        });
        Assert.Equal(false, (object?)await dialog.WaitAsync(Deadline));
        Assert.Equal(1, MutationCount(page));
    }

    private static int MutationCount(EditorTestContext page) => page.Users.Updates.Count + page.Roles.Updates.Count + page.Tenants.Updates.Count;

    private static Task<dynamic> Open(EditorTestContext page, string kind, string id) => kind switch
    {
        "user" => page.Dialogs.OpenAsync<CreateOrUpdateUser>(id, new Dictionary<string, object>
        {
            [nameof(CreateOrUpdateUser.UserInput)] = new UserDto
            { Id = id, FirstName = "Existing", LastName = "User", Email = "user@example.test", IsActive = true }
        }),
        "role" => page.Dialogs.OpenAsync<CreateOrUpdateRole>(id, new Dictionary<string, object>
        {
            [nameof(CreateOrUpdateRole.UserDetails)] = new RoleDto { Id = id, Name = "Editor", Description = "Original" }
        }),
        "tenant" => page.Dialogs.OpenAsync<CreateOrUpdateTenant>(id, new Dictionary<string, object>
        {
            [nameof(CreateOrUpdateTenant.TenantInput)] = new TenantDto
            {
                Id = id, Identifier = id, Name = "Tenant", AdminEmail = "admin@example.test", IsActive = true,
                ValidUpto = new DateTime(2030, 6, 15, 12, 0, 0)
            }
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static EditorForm Form(EditorTestContext page, string kind)
    {
        if (kind == "user")
        {
            var form = page.All.OfType<RadzenTemplateForm<CreateUserRequest>>().Last();
            return new(() => form.Submit.InvokeAsync(form.Data), value => form.Data.FirstName = value,
                () => form.Data.FirstName, () => EscapeAsync(form.Attributes["onkeydown"]));
        }
        if (kind == "role")
        {
            var form = page.All.OfType<RadzenTemplateForm<CreateOrUpdateRoleRequest>>().Last();
            return new(() => form.Submit.InvokeAsync(form.Data), value => form.Data.Description = value,
                () => form.Data.Description, () => EscapeAsync(form.Attributes["onkeydown"]));
        }
        var tenant = page.All.OfType<RadzenTemplateForm<CreateOrUpdateTenantRequest>>().Last();
        return new(() => tenant.Submit.InvokeAsync(tenant.Data), value => tenant.Data.Name = value,
            () => tenant.Data.Name, () => EscapeAsync(tenant.Attributes["onkeydown"]));
    }

    private static Task EscapeAsync(object callback) => callback is EventCallback<KeyboardEventArgs> typed
        ? typed.InvokeAsync(new KeyboardEventArgs { Key = "Escape", Code = "Escape" })
        : Assert.IsType<EventCallback>(callback).InvokeAsync(new KeyboardEventArgs { Key = "Escape", Code = "Escape" });

    private sealed record EditorForm(Func<Task> Submit, Action<string> Edit, Func<string?> Value, Func<Task> Escape);
}
