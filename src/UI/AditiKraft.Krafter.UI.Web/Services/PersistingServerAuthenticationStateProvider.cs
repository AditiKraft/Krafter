using System.Diagnostics;
using System.Security.Claims;
using AditiKraft.Krafter.Shared.Common.Auth;
using AditiKraft.Krafter.Shared.Common.Extensions;
using AditiKraft.Krafter.Shared.Common.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;

namespace AditiKraft.Krafter.UI.Web.Services;

public class PersistingServerAuthenticationStateProvider : ServerAuthenticationStateProvider, IDisposable
{
    private readonly PersistentComponentState _state;
    private readonly PersistingComponentStateSubscription _subscription;
    private Task<AuthenticationState>? _authenticationStateTask;

    public PersistingServerAuthenticationStateProvider(
        PersistentComponentState persistentComponentState)
    {
        _state = persistentComponentState;
        AuthenticationStateChanged += OnAuthenticationStateChanged;
        _subscription = _state.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveWebAssembly);
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task) => _authenticationStateTask = task;

    private async Task OnPersistingAsync()
    {
        if (_authenticationStateTask is null)
        {
            throw new UnreachableException($"Authentication state not set in {nameof(OnPersistingAsync)}().");
        }

        AuthenticationState authenticationState = await _authenticationStateTask;
        ClaimsPrincipal principal = authenticationState.User;

        if (principal.Identity?.IsAuthenticated == true)
        {
            string? userId = principal.GetUserId();
            string? email = principal.GetEmail();
            string? firstName = principal.GetFirstName();
            string? lastName = principal.GetSurname();
            List<string> roles = principal.GetRoles();
            var permissions = principal.FindAll(KrafterClaims.Permission).Select(c => c.Value).ToList();

            _state.PersistAsJson(nameof(UserInfo),
                new UserInfo
                {
                    Id = userId,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    Roles = roles,
                    Permissions = permissions
                });
        }
    }

    public void Dispose()
    {
        _subscription.Dispose();
        AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}
