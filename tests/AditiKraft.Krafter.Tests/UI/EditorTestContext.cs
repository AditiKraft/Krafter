using System.Security.Claims;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using AditiKraft.Krafter.UI.Web.Client.Features.Roles;
using AditiKraft.Krafter.UI.Web.Client.Features.Roles.Common;
using AditiKraft.Krafter.UI.Web.Client.Features.Tenants;
using AditiKraft.Krafter.UI.Web.Client.Features.Users;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace AditiKraft.Krafter.Tests.UI;

internal sealed class EditorTestContext : IAsyncDisposable
{
    private readonly ServiceProvider services;
    private readonly HtmlRenderer renderer;
    private readonly Components components;
    public ControlledUsersApi Users { get; } = new();
    public ControlledRolesApi Roles { get; } = new();
    public ControlledTenantsApi Tenants { get; } = new();
    public DialogService Dialogs => services.GetRequiredService<DialogService>();
    public IEnumerable<IComponent> All => components.All;

    public EditorTestContext(string[]? permissions = null)
    {
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddSingleton<NavigationManager, TestNavigation>();
        registrations.AddSingleton<IJSRuntime, NoJavaScript>();
        registrations.AddRadzenComponents();
        registrations.AddAuthorizationCore(PermissionRegistration.RegisterPermissionClaims);
        registrations.AddSingleton<AuthenticationStateProvider>(new EditorAuthenticationState(permissions ??
        [
            "Permissions.UserRoles.View", "Permissions.UserRoles.Update",
            "Permissions.RoleClaims.View", "Permissions.RoleClaims.Update"
        ]));
        registrations.AddSingleton(new AppUrls());
        registrations.AddSingleton<ApiCallService>();
        registrations.AddSingleton<IUsersApi>(Users);
        registrations.AddSingleton<IRolesApi>(Roles);
        registrations.AddSingleton<ITenantsApi>(Tenants);
        registrations.AddSingleton<Components>();
        registrations.AddSingleton<IComponentActivator>(provider => provider.GetRequiredService<Components>());
        services = registrations.BuildServiceProvider();
        components = services.GetRequiredService<Components>();
        renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
    }

    public RadzenTemplateForm<CreateUserRequest> UserForm => All.OfType<RadzenTemplateForm<CreateUserRequest>>().Last();
    public MultiSelectRoleDropDownDataGrid RoleSelection => All.OfType<MultiSelectRoleDropDownDataGrid>().Last();
    public RadzenDropDownDataGrid<IEnumerable<string>> RoleSelector => All.OfType<RadzenDropDownDataGrid<IEnumerable<string>>>().Last();
    public RadzenButton Button(string text) => All.OfType<RadzenButton>().Last(button => button.Text == text);
    public Task RunAsync(Action action) => renderer.Dispatcher.InvokeAsync(action);
    public Task RunAsync(Func<Task> action) => renderer.Dispatcher.InvokeAsync(action);
    public Task<HtmlRootComponent> RenderAsync<T>() where T : IComponent =>
        renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<T>());
    public Task<HtmlRootComponent> BeginAsync<T>(Dictionary<string, object?> parameters) where T : IComponent =>
        renderer.Dispatcher.InvokeAsync(() => renderer.BeginRenderingComponent<T>(ParameterView.FromDictionary(parameters)));

    public async ValueTask DisposeAsync()
    {
        await renderer.DisposeAsync();
        await services.DisposeAsync();
    }

    private sealed class Components(IServiceProvider services) : IComponentActivator
    {
        public List<IComponent> All { get; } = [];
        public IComponent CreateInstance(Type type)
        {
            var component = (IComponent)ActivatorUtilities.CreateInstance(services, type);
            All.Add(component);
            return component;
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://app.example.test/", "https://app.example.test/users");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}

internal sealed class EditorAuthenticationState(string[] permissions) : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
        new ClaimsPrincipal(new ClaimsIdentity(permissions.Select(permission => new Claim(AppClaimTypes.Permission, permission)), "test"))));
}

internal sealed class ControlledRolesApi : IRolesApi
{
    public List<CreateOrUpdateRoleRequest> Updates { get; } = [];
    public Func<string, CreateOrUpdateRoleRequest, Task<Response>> Save { get; set; } = (_, _) => Task.FromResult(Response.Success());
    public Task<Response<RoleDto>> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Response<RoleDto>.Success(new RoleDto { Id = roleId, Permissions = ["Permissions.Users.View"] }));
    public Task<Response> UpdateRoleAsync(string id, CreateOrUpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        Updates.Add(request);
        return Save(id, request);
    }
    public Task<Response<PaginationResponse<RoleDto>>> GetRolesAsync(GetRequestInput request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Response<PaginationResponse<RoleDto>>.Success(new PaginationResponse<RoleDto>(
        [
            new RoleDto { Id = "existing-a", Name = "ExistingA" },
            new RoleDto { Id = "existing-b", Name = "ExistingB" },
            new RoleDto { Id = "added-c", Name = "AddedC" }
        ], 3, 0, 10)));
    public Task<Response> CreateRoleAsync(CreateOrUpdateRoleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Response> DeleteRoleAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Response> UpdateRolePermissionsAsync(UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class ControlledUsersApi : IUsersApi
{
    public List<CreateUserRequest> Updates { get; } = [];
    public int LoadCount { get; private set; }
    public Func<Task<Response<List<UserRoleDto>>>> Load { get; set; } = () => Task.FromResult(Memberships());
    public Func<string, CreateUserRequest, Task<Response>> Save { get; set; } = (_, _) => Task.FromResult(Response.Success());
    public static Response<List<UserRoleDto>> Memberships() => Response<List<UserRoleDto>>.Success(
    [
        new UserRoleDto { RoleId = "existing-a", RoleName = "ExistingA", Enabled = true },
        new UserRoleDto { RoleId = "existing-b", RoleName = "ExistingB", Enabled = true }
    ]);
    public Task<Response<List<UserRoleDto>>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        LoadCount++;
        return Load();
    }
    public Task<Response> UpdateUserAsync(string id, CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        Updates.Add(request);
        return Save(id, request);
    }
    public Task<Response<PaginationResponse<UserDto>>> GetUsersAsync(GetRequestInput request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Response<PaginationResponse<UserInfo>>> GetUsersByRoleAsync(string roleId, GetRequestInput request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Response> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Response> DeleteUserAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Response<List<string>>> GetPermissionsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Response> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Response> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Response> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class ControlledTenantsApi : ITenantsApi
{
    public List<CreateOrUpdateTenantRequest> Updates { get; } = [];
    public Func<string, CreateOrUpdateTenantRequest, Task<Response>> Save { get; set; } = (_, _) => Task.FromResult(Response.Success());
    public Task<Response> UpdateTenantAsync(string id, CreateOrUpdateTenantRequest request, CancellationToken cancellationToken = default)
    {
        Updates.Add(request);
        return Save(id, request);
    }
    public Task<Response> CreateTenantAsync(CreateOrUpdateTenantRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Response<PaginationResponse<TenantDto>>> GetTenantsAsync(GetRequestInput request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Response> DeleteTenantAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
