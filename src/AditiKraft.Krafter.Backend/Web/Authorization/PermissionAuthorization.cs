using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Extensions;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AditiKraft.Krafter.Backend.Web.Authorization;

internal class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; private set; } = permission;
}

internal class PermissionAuthorizationHandler(
    IServiceScopeFactory scopeFactory,
    ITenantGetterService tenantGetterService) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        string? userId = context.User?.GetUserId();
        if (userId is null)
        {
            return;
        }

        // Fast path: check permission claims enriched by BlazorJwtBearerEvents.TokenValidated (SSR)
        if (context.User!.HasClaim(AppClaimTypes.Permission, requirement.Permission))
        {
            context.Succeed(requirement);
            return;
        }

        CurrentTenantDetails tenant = tenantGetterService.Tenant;
        if (string.IsNullOrWhiteSpace(tenant.Id) || string.IsNullOrWhiteSpace(tenant.TenantLink))
        {
            return;
        }

        // Concurrent Blazor authorization checks need separate DbContexts for the same tenant.
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ITenantSetterService tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantSetterService>();
        tenantSetter.SetTenant(tenant);

        IUserService userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        if ((await userService.HasPermissionAsync(userId, requirement.Permission)).Data)
        {
            context.Succeed(requirement);
        }
    }
}

internal class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private DefaultAuthorizationPolicyProvider FallbackPolicyProvider { get; } = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => FallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(AppClaimTypes.Permission, StringComparison.OrdinalIgnoreCase))
        {
            var policy = new AuthorizationPolicyBuilder();
            policy.AddRequirements(new PermissionRequirement(policyName));
            return Task.FromResult<AuthorizationPolicy?>(policy.Build());
        }

        return FallbackPolicyProvider.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => Task.FromResult<AuthorizationPolicy?>(null);
}

public static class MustHavePermissionExtension
{
    public static TBuilder MustHavePermission<TBuilder>(this TBuilder builder, string action, string resource)
        where TBuilder : IEndpointConventionBuilder
    {
        string policyName = PermissionDefinition.NameFor(action, resource);
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        ArgumentNullException.ThrowIfNull(policyName);
        return builder.RequireAuthorization(policyName);
    }
}



