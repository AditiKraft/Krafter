using System.Security.Claims;

namespace AditiKraft.Krafter.Backend.Common.Auth;

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
