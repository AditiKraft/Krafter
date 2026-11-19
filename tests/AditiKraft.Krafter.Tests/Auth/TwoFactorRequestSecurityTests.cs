using AditiKraft.Krafter.Backend.Features.Auth.Common;
using Microsoft.AspNetCore.Http;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class TwoFactorRequestSecurityTests
{
    [Theory]
    [InlineData("https://app.example.com", "1", true)]
    [InlineData("https://other.example.com", "1", false)]
    [InlineData("https://app.example.com", "", false)]
    [InlineData("null", "1", false)]
    public void CookieMutationsRequireTheNonSimpleHeaderAndMatchingOrigin(string origin, string header, bool accepted)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("app.example.com");
        context.Request.Headers.Origin = origin;
        context.Request.Headers["X-Krafter-Mfa"] = header;
        Assert.Equal(accepted, TwoFactorEndpoints.IsSafeRequest(context));
    }

    [Theory]
    [InlineData(null, "password")]
    [InlineData("user@example.com", null)]
    public async Task MalformedPrimaryCredentialsFailWithoutAnException(string? email, string? password)
    {
        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var response = await scope.Mfa.PasswordLoginAsync(new() { Email = email!, Password = password!, IsExternalLogin = true }, "test");
        Assert.True(response.IsError);
        Assert.Equal(401, response.StatusCode);
    }
}
