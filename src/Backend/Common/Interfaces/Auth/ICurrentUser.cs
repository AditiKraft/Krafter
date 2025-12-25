using System.Security.Claims;

namespace Backend.Common.Interfaces.Auth;

public interface ICurrentUser
{
    public string? Name { get; }

    public string GetUserId();

    public string? GetUserEmail();

    // string? GetTenant();

    public bool IsAuthenticated();

    public bool IsInRole(string role);

    public IEnumerable<Claim>? GetUserClaims();
}

public interface ICurrentUserInitializer
{
    public void SetCurrentUser(ClaimsPrincipal user);

    public void SetCurrentUserId(string userId);
}
