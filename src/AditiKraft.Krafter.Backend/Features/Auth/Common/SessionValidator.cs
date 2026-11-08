using System.Security.Claims;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using Microsoft.AspNetCore.Identity;
using AditiKraft.Krafter.Backend.Web.Configuration;
using Microsoft.Extensions.Options;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public sealed class SessionValidator(UserManager<ApplicationUser> users, ITenantGetterService tenant, IOptions<SecuritySettings> security)
{
    public async Task<bool> ValidateAsync(ClaimsPrincipal principal)
    {
        string? id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(id) || principal.FindFirstValue(AppClaimTypes.TenantId) != tenant.Tenant.Id ||
            !tenant.Tenant.IsActive || tenant.Tenant.IsDeleted || tenant.Tenant.ValidUpto < DateTime.UtcNow ||
            !long.TryParse(principal.FindFirstValue(AppClaimTypes.AuthenticationTime), out long authenticatedAt) || authenticatedAt <= 0)
            return false;
        ApplicationUser? user = await users.FindByIdAsync(id);
        return user is { IsActive: true, IsDeleted: false } && !await users.IsLockedOutAsync(user) &&
            (!security.Value.RequireConfirmedAccount || user.EmailConfirmed) &&
            int.TryParse(principal.FindFirstValue(AppClaimTypes.AuthenticationVersion), out int version) && version == user.AuthenticationVersion &&
            (!user.TwoFactorEnabled || principal.FindFirstValue(AppClaimTypes.Mfa) == "true");
    }
}
