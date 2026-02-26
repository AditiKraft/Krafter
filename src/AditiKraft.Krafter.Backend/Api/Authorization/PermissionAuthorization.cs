using AditiKraft.Krafter.Backend.Features.Users._Shared;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AditiKraft.Krafter.Backend.Api.Authorization;

internal class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; private set; } = permission;
}

internal class PermissionAuthorizationHandler(IUserService userService) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User?.GetUserId() is { } userId &&
            (await userService.HasPermissionAsync(userId, requirement.Permission)).Data)
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
