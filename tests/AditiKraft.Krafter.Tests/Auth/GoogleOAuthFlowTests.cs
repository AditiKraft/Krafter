using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class GoogleOAuthFlowTests
{
    [Theory]
    [InlineData("https://app.example.test", "root")]
    [InlineData("https://blue.example.test", "tenant-blue")]
    public async Task RootAndRegisteredTenantFlowsRequirePkceAndBothBrowserProofs(string origin, string tenantId)
    {
        var fixture = new Fixture();
        GoogleLoginStartResponse start = await fixture.Start(origin);
        var authorization = QueryHelpers.ParseQuery(new Uri(start.AuthorizationUrl).Query);
        Assert.Equal("https://app.example.test/google-callback", authorization["redirect_uri"]);
        Assert.Equal("S256", authorization["code_challenge_method"]);
        Assert.Equal(start.State, authorization["state"]);
        Assert.DoesNotContain("|||", start.State);
        Assert.True((await fixture.Callback(start, new string('a', 64))).IsError);
        Assert.Equal(0, fixture.Provider.Calls);
        Response<GoogleLoginCallbackResponse> completed = await fixture.Callback(start);
        Assert.False(completed.IsError);
        Assert.Equal(1, fixture.Provider.Calls);
        Assert.Equal(authorization["code_challenge"], WebEncoders.Base64UrlEncode(
            SHA256.HashData(Encoding.ASCII.GetBytes(fixture.Provider.LastState!.CodeVerifier))));
        Assert.Equal(tenantId, fixture.Provider.LastState.TenantId);
        string completion = completed.Data!.CompletionUrl;
        Assert.StartsWith(origin + "/google-complete#handoff=", completion);
        Assert.DoesNotContain("provider-code", completion);
        Assert.DoesNotContain(fixture.Provider.LastState.CodeVerifier, completion);
        string handoff = Handoff(completion);
        Assert.True((await fixture.Flow.CompleteAsync(new() { Handoff = handoff, BrowserNonce = fixture.Nonce }, "wrong-tenant", default)).IsError);
        Assert.True((await fixture.Flow.CompleteAsync(new() { Handoff = handoff, BrowserNonce = new string('b', 64) }, tenantId, default)).IsError);
        var request = new GoogleAuthRequest { Handoff = handoff, BrowserNonce = fixture.Nonce };
        Response<LoginResponse> login = await fixture.Flow.CompleteAsync(request, tenantId, default);
        Assert.False(login.IsError);
        Assert.True(login.Data!.IsCompleted);
        Assert.True((await fixture.Flow.CompleteAsync(request, tenantId, default)).IsError);
        Assert.True((await fixture.Callback(start)).IsError);
        Assert.Equal(1, fixture.Provider.Calls);
    }

    [Theory]
    [InlineData("https://attacker.example.test")]
    [InlineData("https://nested.blue.example.test")]
    [InlineData("https://blue.example.test:444")]
    [InlineData("http://blue.example.test")]
    [InlineData("https://api.example.test")]
    [InlineData("https://blue.example.test/path")]
    [InlineData("https://unrelated.test")]
    public async Task UnregisteredOrNoncanonicalOriginsCannotCreateProviderFlows(string origin)
    {
        var fixture = new Fixture();
        Response<GoogleLoginStartResponse> result = await fixture.Flow.StartAsync(fixture.Request(origin), default);
        Assert.True(result.IsError);
        Assert.Empty(fixture.Store.Rows);
        Assert.Equal(0, fixture.Provider.Calls);
    }

    [Fact]
    public async Task MissingOrExpiredCorrelationNeverCallsTheProvider()
    {
        var fixture = new Fixture();
        GoogleLoginStartResponse start = await fixture.Start();
        Assert.True((await fixture.Callback(start, "")).IsError);
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        Assert.True((await fixture.Callback(start)).IsError);
        Assert.Equal(0, fixture.Provider.Calls);
    }

    [Fact]
    public async Task TenantReassignmentDuringConsentCannotAuthenticateTheReplacementTenant()
    {
        var fixture = new Fixture();
        GoogleLoginStartResponse start = await fixture.Start();
        fixture.Tenants.BlueId = "replacement";
        Response<GoogleLoginCallbackResponse> response = await fixture.Callback(start);
        Assert.True(response.IsError);
        Assert.Null(response.Data);
        Assert.Equal(0, fixture.Provider.Calls);
    }

    [Theory]
    [InlineData("https://app.example.test")]
    [InlineData("https://blue.example.test")]
    public async Task VerifiedLoginFailureKeepsItsTenantAndReturnPathWithoutCreatingAHandoff(string origin)
    {
        var fixture = new Fixture();
        fixture.Provider.Result = Response<LoginResponse>.Unauthorized("Private provider/account failure detail.");
        GoogleLoginStartRequest request = fixture.Request(origin);
        request.ReturnUrl = "/users?filter=active#details";
        GoogleLoginStartResponse start = (await fixture.Flow.StartAsync(request, default)).Data!;

        Response<GoogleLoginCallbackResponse> response = await fixture.Callback(start);

        AssertLoginFailure(response, origin, request.ReturnUrl);
        Assert.Equal(1, fixture.Provider.Calls);
        Assert.Empty(fixture.Store.Rows);
        Assert.True((await fixture.Callback(start)).IsError);
        Assert.Equal(1, fixture.Provider.Calls);
    }

    [Theory]
    [InlineData("//attacker.test/path")]
    [InlineData("/\t/attacker.test/path")]
    [InlineData("/google-complete#handoff=untrusted")]
    public async Task VerifiedLoginFailureNormalizesUnsafeReturnPaths(string returnUrl)
    {
        var fixture = new Fixture();
        fixture.Provider.Result = Response<LoginResponse>.Unauthorized("Unable to sign in.");
        GoogleLoginStartRequest request = fixture.Request("https://blue.example.test");
        request.ReturnUrl = returnUrl;
        GoogleLoginStartResponse start = (await fixture.Flow.StartAsync(request, default)).Data!;

        AssertLoginFailure(await fixture.Callback(start), "https://blue.example.test", "/");
    }

    [Fact]
    public async Task ProviderRefusalRequiresValidStateBeforeReturningToTheRecordedTenant()
    {
        var fixture = new Fixture();
        GoogleLoginStartResponse start = await fixture.Start();
        var request = new GoogleLoginCallbackRequest { State = start.State, Code = "", RootCorrelation = new string('A', 64) };
        Response<GoogleLoginCallbackResponse> rejected = await fixture.Flow.CallbackAsync(request, default);
        Assert.True(rejected.IsError);
        Assert.Null(rejected.Data);
        Assert.Single(fixture.Store.Rows);

        request.RootCorrelation = fixture.Correlation;
        AssertLoginFailure(await fixture.Flow.CallbackAsync(request, default), "https://blue.example.test", "/users");
        Assert.Empty(fixture.Store.Rows);
        Assert.Equal(0, fixture.Provider.Calls);
        Assert.True((await fixture.Flow.CallbackAsync(request, default)).IsError);
    }

    [Fact]
    public async Task ProviderTransportFailuresReturnOnlyTheVerifiedGenericFailureDestination()
    {
        foreach (Exception failure in new Exception[]
        {
            new HttpRequestException("Private provider detail."),
            new JsonException("Private provider detail."),
            new TaskCanceledException("Private provider timeout detail.")
        })
        {
            var fixture = new Fixture();
            fixture.Provider.Failure = failure;
            GoogleLoginStartResponse start = await fixture.Start();
            AssertLoginFailure(await fixture.Callback(start), "https://blue.example.test", "/users");
            Assert.Empty(fixture.Store.Rows);
        }
    }

    [Fact]
    public async Task CallerCancellationDoesNotBecomeAFailureRedirect()
    {
        var fixture = new Fixture();
        GoogleLoginStartResponse start = await fixture.Start();
        using var cancellation = new CancellationTokenSource();
        fixture.Provider.Failure = new OperationCanceledException(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Flow.CallbackAsync(new()
        { State = start.State, Code = "provider-code", RootCorrelation = fixture.Correlation }, cancellation.Token));
    }

    [Fact]
    public async Task ConcurrentCallbacksExchangeTheCodeOnceAndEnrolledGoogleLoginStillRequiresMfa()
    {
        var fixture = new Fixture();
        fixture.Provider.Result = Response<LoginResponse>.Success(LoginResponse.Pending("local-mfa", fixture.Clock.GetUtcNow().AddMinutes(5)));
        GoogleLoginStartResponse start = await fixture.Start();
        Response<GoogleLoginCallbackResponse>[] callbacks = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => fixture.Callback(start)));
        Response<GoogleLoginCallbackResponse> success = Assert.Single(callbacks, x => !x.IsError);
        Assert.Equal(1, fixture.Provider.Calls);
        Response<LoginResponse> result = await fixture.Flow.CompleteAsync(new()
        { Handoff = Handoff(success.Data!.CompletionUrl), BrowserNonce = fixture.Nonce }, "tenant-blue", default);
        Assert.False(result.IsError);
        Assert.True(result.Data!.RequiresTwoFactor);
        Assert.Null(result.Data.Tokens);
    }

    [Fact]
    public async Task LegacyRawCodeRequestsAndExpiredHandoffsAreRejected()
    {
        var fixture = new Fixture();
        var legacy = JsonSerializer.Deserialize<GoogleAuthRequest>("""{"Code":"provider-code"}""")!;
        Assert.True((await fixture.Flow.CompleteAsync(legacy, "tenant-blue", default)).IsError);
        GoogleLoginStartResponse start = await fixture.Start();
        Response<GoogleLoginCallbackResponse> callback = await fixture.Callback(start);
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.True((await fixture.Flow.CompleteAsync(new()
        { Handoff = Handoff(callback.Data!.CompletionUrl), BrowserNonce = fixture.Nonce }, "tenant-blue", default)).IsError);
    }

    [Theory]
    [InlineData("//attacker.test/path", "/")]
    [InlineData("/\t/attacker.test/path", "/")]
    [InlineData("/users?filter=active#details", "/users?filter=active#details")]
    public async Task ServerTransactionKeepsOnlySafeLocalReturnPaths(string target, string expected)
    {
        var fixture = new Fixture();
        GoogleLoginStartRequest request = fixture.Request("https://blue.example.test");
        request.ReturnUrl = target;
        GoogleLoginStartResponse start = (await fixture.Flow.StartAsync(request, default)).Data!;
        string completed = (await fixture.Callback(start)).Data!.CompletionUrl;
        Assert.Equal(expected, QueryHelpers.ParseQuery("?" + new Uri(completed).Fragment.TrimStart('#'))["returnUrl"]);
    }

    private static string Handoff(string url) => QueryHelpers.ParseQuery("?" + new Uri(url).Fragment.TrimStart('#'))["handoff"].ToString();

    private static void AssertLoginFailure(Response<GoogleLoginCallbackResponse> response, string origin, string returnUrl)
    {
        Assert.False(response.IsError);
        Uri destination = new(response.Data!.CompletionUrl);
        Assert.Equal(origin, destination.GetLeftPart(UriPartial.Authority));
        Assert.Equal("/login", destination.AbsolutePath);
        var query = QueryHelpers.ParseQuery(destination.Query);
        Assert.Equal(2, query.Count);
        Assert.Equal("google-auth-failed", query["error"]);
        Assert.Equal(returnUrl, query["ReturnUrl"]);
        Assert.Empty(destination.Fragment);
    }

    private sealed class Fixture
    {
        public MfaClock Clock { get; } = new();
        public Registry Tenants { get; } = new();
        public FakeProvider Provider { get; } = new();
        public MemoryTransactions Store { get; }
        public GoogleOAuthFlow Flow { get; }
        public string Nonce { get; } = GoogleLoginTransactionStore.NewSecret();
        public string Correlation { get; } = GoogleLoginTransactionStore.NewSecret();
        public Fixture()
        {
            Store = new(Clock);
            Flow = new(Store, Tenants, Provider, new AppUrls
            { RootUiUrl = "https://app.example.test", TenantBaseDomain = "example.test" },
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Authentication:Google:ClientId"] = "test-client" }).Build(), Clock);
        }
        public GoogleLoginStartRequest Request(string origin) => new()
        { UiOrigin = origin, ReturnUrl = "/users", ClientNonceHash = GoogleLoginTransactionStore.Hash(Nonce), RootCorrelationHash = GoogleLoginTransactionStore.Hash(Correlation) };
        public async Task<GoogleLoginStartResponse> Start(string origin = "https://blue.example.test")
        {
            Response<GoogleLoginStartResponse> response = await Flow.StartAsync(Request(origin), default);
            Assert.False(response.IsError);
            return response.Data!;
        }
        public Task<Response<GoogleLoginCallbackResponse>> Callback(GoogleLoginStartResponse start, string? proof = null) =>
            Flow.CallbackAsync(new() { State = start.State, Code = "provider-code", RootCorrelation = proof ?? Correlation }, default);
    }

    private sealed class Registry : ITenantFinderService
    {
        public string BlueId { get; set; } = "tenant-blue";
        public Task<Response<Tenant>> Find(string? identifier, CancellationToken cancellationToken = default) => Task.FromResult(identifier is "root" or "blue"
            ? Response<Tenant>.Success(new() { Id = identifier == "root" ? "root" : BlueId, Identifier = identifier, IsActive = true, ValidUpto = DateTime.MaxValue })
            : Response<Tenant>.NotFound("Tenant not found"));
    }

    private sealed class FakeProvider : IGoogleAccountAuthenticator
    {
        public int Calls;
        public GoogleLoginState? LastState;
        public Response<LoginResponse> Result { get; set; } = Response<LoginResponse>.Success(LoginResponse.Completed(TokenTestData.Create()));
        public Exception? Failure { get; set; }
        public Task<Response<LoginResponse>> AuthenticateAsync(GoogleLoginState state, string code, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            LastState = state;
            return Failure is null ? Task.FromResult(Result) : Task.FromException<Response<LoginResponse>>(Failure);
        }
    }

    private sealed class MemoryTransactions(TimeProvider clock) : IGoogleLoginTransactionStore
    {
        public ConcurrentDictionary<string, (string Purpose, string Tenant, string ProofHash, object Payload, DateTimeOffset Expires)> Rows { get; } = new();
        public Task<string> CreateAsync<T>(string purpose, string tenantId, string proofHash, T payload, DateTimeOffset expiresAt, CancellationToken cancellationToken)
        { string id = GoogleLoginTransactionStore.NewSecret(); Rows[id] = (purpose, tenantId, proofHash, payload!, expiresAt); return Task.FromResult(id); }
        public Task<T?> TakeAsync<T>(string id, string purpose, string proof, string? tenantId, CancellationToken cancellationToken) where T : class
        {
            if (!GoogleLoginTransactionStore.IsSecret(id) || !GoogleLoginTransactionStore.IsSecret(proof) || !Rows.TryGetValue(id, out var row) ||
                row.Purpose != purpose || row.ProofHash != GoogleLoginTransactionStore.Hash(proof) || row.Expires <= clock.GetUtcNow() ||
                (tenantId is not null && tenantId != row.Tenant)) return Task.FromResult<T?>(null);
            return Task.FromResult(Rows.TryRemove(id, out row) ? row.Payload as T : null);
        }
    }
}
