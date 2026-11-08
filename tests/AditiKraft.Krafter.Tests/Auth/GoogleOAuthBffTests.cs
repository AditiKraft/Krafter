using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class GoogleOAuthBffTests
{
    [Theory]
    [InlineData("attacker.example.test|||/|||untrusted-review-state")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task SyntheticCodeCannotBeRelayedWithoutTheRootBrowserCookie(string state)
    {
        var api = new RecordingApi();
        DefaultHttpContext context = Context("app.example.test");
        IResult result = await Invoke("CallbackAsync", context, Urls, api, "synthetic-review-code", state, CancellationToken.None);
        Assert.Equal("/login?error=invalid-google-state", Assert.IsType<RedirectHttpResult>(result).Url);
        Assert.Equal(0, api.CallbackCalls);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"]);
    }

    [Fact]
    public async Task RootStartSetsHostOnlyCorrelationAndCallbackUsesItsSecret()
    {
        var api = new RecordingApi();
        DefaultHttpContext start = Context("app.example.test");
        IResult redirected = await Invoke("StartAsync", start, Urls, api, "https://blue.example.test", "/users", new string('A', 64), CancellationToken.None);
        Assert.StartsWith("https://accounts.google.com/", Assert.IsType<RedirectHttpResult>(redirected).Url);
        string cookie = Assert.Single(start.Response.Headers.SetCookie)!;
        Assert.StartsWith("__Host-Krafter.Google.", cookie);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        string cookiePair = cookie.Split(';')[0];
        string proof = cookiePair[(cookiePair.IndexOf('=') + 1)..];
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(proof))), api.StartRequest!.RootCorrelationHash);
        DefaultHttpContext callback = Context("app.example.test");
        callback.Request.Headers.Cookie = cookiePair;
        IResult complete = await Invoke("CallbackAsync", callback, Urls, api, "provider-code", api.State, CancellationToken.None);
        Assert.Equal(api.CompletionUrl, Assert.IsType<RedirectHttpResult>(complete).Url);
        Assert.Equal(proof, api.CallbackRequest!.RootCorrelation);
        Assert.Equal("provider-code", api.CallbackRequest.Code);
        Assert.Equal(1, api.CallbackCalls);
        Assert.Contains("expires=", Assert.Single(callback.Response.Headers.SetCookie)!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ACallbackOnAnotherOriginCannotRedeemEvenACopiedCookie()
    {
        var api = new RecordingApi();
        DefaultHttpContext callback = Context("blue.example.test");
        callback.Request.Headers.Cookie = "__Host-Krafter.Google." + api.State + "=" + new string('A', 64);
        IResult result = await Invoke("CallbackAsync", callback, Urls, api, "provider-code", api.State, CancellationToken.None);
        Assert.Equal("/login?error=invalid-google-state", Assert.IsType<RedirectHttpResult>(result).Url);
        Assert.Equal(0, api.CallbackCalls);
    }

    private static readonly AppUrls Urls = new() { RootUiUrl = "https://app.example.test", TenantBaseDomain = "example.test" };
    private static DefaultHttpContext Context(string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new(host);
        return context;
    }
    private static Task<IResult> Invoke(string method, params object?[] args) =>
        (Task<IResult>)typeof(GoogleOAuthBffEndpoints).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args)!;
    private sealed class RecordingApi : IGoogleOAuthApi
    {
        public string State { get; } = new('B', 64);
        public string CompletionUrl { get; } = "https://blue.example.test/google-complete#handoff=" + new string('C', 64);
        public int CallbackCalls { get; private set; }
        public GoogleLoginStartRequest? StartRequest { get; private set; }
        public GoogleLoginCallbackRequest? CallbackRequest { get; private set; }
        public Task<Response<GoogleLoginStartResponse>> StartAsync(GoogleLoginStartRequest request, CancellationToken cancellationToken)
        {
            StartRequest = request;
            return Task.FromResult(Response<GoogleLoginStartResponse>.Success(new(State,
                "https://accounts.google.com/o/oauth2/v2/auth?state=" + State, DateTimeOffset.UtcNow.AddMinutes(5))));
        }
        public Task<Response<GoogleLoginCallbackResponse>> CallbackAsync(GoogleLoginCallbackRequest request, CancellationToken cancellationToken)
        {
            CallbackCalls++;
            CallbackRequest = request;
            return Task.FromResult(Response<GoogleLoginCallbackResponse>.Success(new(CompletionUrl)));
        }
    }
}
