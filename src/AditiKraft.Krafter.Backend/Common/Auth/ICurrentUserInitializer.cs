using System.Security.Claims;

namespace AditiKraft.Krafter.Backend.Common.Auth;

public interface ICurrentUserInitializer
{
    public void SetCurrentUser(ClaimsPrincipal user);

    public void SetCurrentUserId(string userId);
}
