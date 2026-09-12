using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.UI.Web.Client.Features.Roles;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace AditiKraft.Krafter.Tests.Roles;

public sealed class RoleEditorTests
{
    [Theory]
    [InlineData("error")]
    [InlineData("missing-role")]
    [InlineData("null-permissions")]
    [InlineData("network-error")]
    public async Task FailedPermissionLoadBlocksSaveUntilRetrySucceeds(string failure)
    {
        var api = new RolesApi
        {
            Load = () => failure switch
            {
                "error" => Task.FromResult(new Response<RoleDto> { IsError = true, StatusCode = 403 }),
                "missing-role" => Task.FromResult(new Response<RoleDto> { StatusCode = 200 }),
                "null-permissions" => Task.FromResult(Response<RoleDto>.Success(new RoleDto { Permissions = null })),
                _ => throw new HttpRequestException("Unavailable")
            }
        };
        await using var editor = new Editor(api);
        HtmlRootComponent root = await editor.RenderAsync(ExistingRole());
        await root.QuiescenceTask;

        await editor.RunAsync(async () =>
        {
            Assert.True(editor.Button("Save").Disabled);
            Assert.True(editor.Permissions.Disabled);
            Assert.Contains("Could not load role permissions", root.ToHtmlString());
            editor.Form.Data.Description = "Changed description";
            await editor.Form.Submit.InvokeAsync(editor.Form.Data);
            Assert.Empty(api.Updates);

            api.Load = () => Task.FromResult(Permissions("Permissions.Users.View"));
            await editor.Button("Retry").Click.InvokeAsync(new MouseEventArgs());
            Assert.False(editor.Button("Save").Disabled);
            Assert.False(editor.Permissions.Disabled);
            Assert.DoesNotContain("Could not load role permissions", root.ToHtmlString());
            Assert.Equal("Changed description", editor.Form.Data.Description);
            await editor.Form.Submit.InvokeAsync(editor.Form.Data);
        });

        CreateOrUpdateRoleRequest update = Assert.Single(api.Updates);
        Assert.Equal(new[] { "Permissions.Users.View" }, update.Permissions);
        Assert.Equal(2, api.LoadCount);
    }

    [Fact]
    public async Task PermissionLoadInProgressBlocksSubmission()
    {
        var completion = new TaskCompletionSource<Response<RoleDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new RolesApi { Load = () => completion.Task };
        await using var editor = new Editor(api);
        HtmlRootComponent root = await editor.RenderAsync(ExistingRole());
        try
        {
            await editor.RunAsync(async () =>
            {
                Assert.True(editor.Button("Save").Disabled);
                Assert.True(editor.Permissions.Disabled);
                Assert.Contains("Loading role permissions", root.ToHtmlString());
                await editor.Form.Submit.InvokeAsync(editor.Form.Data);
                Assert.Empty(api.Updates);
            });
        }
        finally
        {
            completion.TrySetResult(Permissions("Permissions.Users.View"));
        }

        await root.QuiescenceTask.WaitAsync(TimeSpan.FromSeconds(10));
        await editor.RunAsync(() =>
        {
            Assert.False(editor.Button("Save").Disabled);
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulLoadAllowsEmptyPermissionSelection(bool initiallyEmpty)
    {
        var api = new RolesApi
        {
            Load = () => Task.FromResult(initiallyEmpty ? Permissions() : Permissions("Permissions.Users.View"))
        };
        await using var editor = new Editor(api);
        HtmlRootComponent root = await editor.RenderAsync(ExistingRole());
        await root.QuiescenceTask;

        await editor.RunAsync(async () =>
        {
            Assert.False(editor.Button("Save").Disabled);
            Assert.False(editor.Permissions.Disabled);
            editor.Form.Data.Permissions = [];
            await editor.Form.Submit.InvokeAsync(editor.Form.Data);
        });

        Assert.Empty(Assert.Single(api.Updates).Permissions);
    }

    [Fact]
    public async Task NewRoleCanBeSavedWithoutLoadingPermissions()
    {
        var api = new RolesApi();
        await using var editor = new Editor(api);
        HtmlRootComponent root = await editor.RenderAsync(new RoleDto { Name = "New role", Description = "New" });
        await root.QuiescenceTask;

        await editor.RunAsync(async () =>
        {
            Assert.False(editor.Button("Save").Disabled);
            await editor.Form.Submit.InvokeAsync(editor.Form.Data);
        });

        Assert.Equal(0, api.LoadCount);
        Assert.Single(api.Creates);
        Assert.Empty(api.Updates);
    }

    private static RoleDto ExistingRole() => new() { Id = "editor", Name = "Editor", Description = "Original" };

    private static Response<RoleDto> Permissions(params string[] permissions) =>
        Response<RoleDto>.Success(new RoleDto { Id = "editor", Permissions = [.. permissions] });

    private sealed class Editor : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly CapturingActivator components;
        private readonly HtmlRenderer renderer;

        public Editor(RolesApi api)
        {
            var registrations = new ServiceCollection();
            registrations.AddLogging();
            registrations.AddSingleton<NavigationManager, TestNavigationManager>();
            registrations.AddSingleton<IJSRuntime, NoJavaScript>();
            registrations.AddSingleton<DialogService>();
            registrations.AddSingleton<NotificationService>();
            registrations.AddSingleton<ApiCallService>();
            registrations.AddSingleton<IRolesApi>(api);
            registrations.AddSingleton<CapturingActivator>();
            registrations.AddSingleton<IComponentActivator>(provider => provider.GetRequiredService<CapturingActivator>());
            services = registrations.BuildServiceProvider();
            components = services.GetRequiredService<CapturingActivator>();
            renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        }

        public RadzenTemplateForm<CreateOrUpdateRoleRequest> Form =>
            components.All.OfType<RadzenTemplateForm<CreateOrUpdateRoleRequest>>().Single();

        public RadzenDropDown<List<string>> Permissions =>
            components.All.OfType<RadzenDropDown<List<string>>>().Single();

        public RadzenButton Button(string text) => components.All.OfType<RadzenButton>().Single(button => button.Text == text);

        public Task<HtmlRootComponent> RenderAsync(RoleDto role) => renderer.Dispatcher.InvokeAsync(() =>
            renderer.BeginRenderingComponent<CreateOrUpdateRole>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(CreateOrUpdateRole.UserDetails)] = role })));

        public Task RunAsync(Func<Task> action) => renderer.Dispatcher.InvokeAsync(action);

        public async ValueTask DisposeAsync()
        {
            await renderer.DisposeAsync();
            await services.DisposeAsync();
        }
    }

    private sealed class CapturingActivator(IServiceProvider services) : IComponentActivator
    {
        public List<IComponent> All { get; } = [];

        public IComponent CreateInstance(Type componentType)
        {
            var component = (IComponent)ActivatorUtilities.CreateInstance(services, componentType);
            All.Add(component);
            return component;
        }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://localhost/", "https://localhost/roles");

        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException("Static rendering must not call JavaScript.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException("Static rendering must not call JavaScript.");
    }

    private sealed class RolesApi : IRolesApi
    {
        public Func<Task<Response<RoleDto>>> Load { get; set; } = () => throw new NotSupportedException();
        public int LoadCount { get; private set; }
        public List<CreateOrUpdateRoleRequest> Updates { get; } = [];
        public List<CreateOrUpdateRoleRequest> Creates { get; } = [];

        public Task<Response<RoleDto>> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken = default)
        {
            LoadCount++;
            return Load();
        }

        public Task<Response> CreateRoleAsync(CreateOrUpdateRoleRequest request, CancellationToken cancellationToken = default)
        {
            Creates.Add(request);
            return Task.FromResult(new Response());
        }

        public Task<Response> UpdateRoleAsync(string id, CreateOrUpdateRoleRequest request,
            CancellationToken cancellationToken = default)
        {
            Updates.Add(request);
            return Task.FromResult(new Response());
        }

        public Task<Response<PaginationResponse<RoleDto>>> GetRolesAsync(GetRequestInput request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Response> DeleteRoleAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Response> UpdateRolePermissionsAsync(UpdateRolePermissionsRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
