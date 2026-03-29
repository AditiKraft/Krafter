using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AditiKraft.Krafter.Backend.Web.Authorization;

internal class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; private set; } = permission;
}

internal class PermissionAuthorizationHandler(IServiceScopeFactory scopeFactory) : AuthorizationHandler<PermissionRequirement>
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
        if (context.User!.HasClaim(KrafterClaims.Permission, requirement.Permission))
        {
            context.Succeed(requirement);
            return;
        }

        // Slow path: query DB in a new scope to avoid DbContext concurrency issues
        // during Blazor SSR where multiple AuthorizeView components trigger concurrent checks
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
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
        if (policyName.StartsWith(KrafterClaims.Permission, StringComparison.OrdinalIgnoreCase))
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
        string policyName = KrafterPermission.NameFor(action, resource);
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        ArgumentNullException.ThrowIfNull(policyName);
        return builder.RequireAuthorization(policyName);
    }
}



