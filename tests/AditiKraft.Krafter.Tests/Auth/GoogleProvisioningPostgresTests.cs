using System.Net;
using System.Net.Http.Json;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Auth;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AditiKraft.Krafter.Tests.Auth;

[Collection("MFA PostgreSQL")]
public sealed class GoogleProvisioningPostgresTests
{
    private const string Email = "google-provisioning@example.test";

    [PostgresMfaFact]
    public Task DeletedEmailCannotBeRecreatedAndFailureReturnsToTheVerifiedTenantAsync() =>
        VerifyDeletedIdentifierAsync(reserveUsernameOnly: false);

    [PostgresMfaFact]
    public Task DeletedUsernameCannotBeRecreatedAndFailureReturnsToTheVerifiedTenantAsync() =>
        VerifyDeletedIdentifierAsync(reserveUsernameOnly: true);

    private static async Task VerifyDeletedIdentifierAsync(bool reserveUsernameOnly)
    {
        using var harness = new MfaHarness(Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!);
        string tenantId = Guid.NewGuid().ToString();
        using MfaScope setup = harness.Scope(tenantId);
        await setup.Db.Database.MigrateAsync();
        ApplicationRole basic = await AddBasicRoleAsync(setup);
        var deleted = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(), IsActive = true, EmailConfirmed = true,
            Email = reserveUsernameOnly ? "retained@example.test" : Email.ToUpperInvariant(),
            UserName = Email.ToUpperInvariant()
        };
        Assert.True((await setup.Users.CreateAsync(deleted)).Succeeded);
        setup.Db.UserRoles.Add(new ApplicationUserRole { UserId = deleted.Id, RoleId = basic.Id });
        await setup.Db.SaveChangesAsync();
        Assert.False((await new DeleteUser.Handler(setup.Users, setup.Db).DeleteAsync(deleted.Id)).IsError);
        int deletedVersion = deleted.AuthenticationVersion;

        using MfaScope operation = harness.Scope(tenantId);
        using var transport = new GoogleTransport();
        using var client = new HttpClient(transport);
        var authenticator = new ProvisioningAuthenticator(Handler(operation, client));
        var state = new GoogleLoginState(tenantId, "blue", "https://blue.example.test",
            "/users?filter=active#details", new string('A', 64), new string('v', 43));
        var flow = new GoogleOAuthFlow(new AuthorizationState(state), new Registry(tenantId), authenticator,
            new AppUrls { RootUiUrl = "https://app.example.test", TenantBaseDomain = "example.test" },
            Configuration(), harness.Clock);

        Response<GoogleLoginCallbackResponse> callback = await flow.CallbackAsync(new()
        { State = new string('B', 64), RootCorrelation = new string('C', 64), Code = "provider-code" }, default);

        Assert.False(callback.IsError);
        Uri destination = new(callback.Data!.CompletionUrl);
        Assert.Equal("https://blue.example.test", destination.GetLeftPart(UriPartial.Authority));
        Assert.Equal("/login", destination.AbsolutePath);
        var query = QueryHelpers.ParseQuery(destination.Query);
        Assert.Equal("google-auth-failed", query["error"]);
        Assert.Equal(state.ReturnUrl, query["ReturnUrl"]);
        Assert.Equal(401, authenticator.Result!.StatusCode);
        Assert.Null(authenticator.Result.Data);
        Assert.Null(operation.Db.Database.CurrentTransaction);
        Assert.False(operation.Db.ChangeTracker.HasChanges());
        // A caller's later save must not insert a rejected tracked account.
        await operation.Db.SaveChangesAsync();

        using MfaScope check = harness.Scope(tenantId);
        ApplicationUser retained = Assert.Single(await check.Db.Users.IgnoreQueryFilters()
            .Where(user => user.TenantId == tenantId).ToListAsync());
        Assert.Equal(deleted.Id, retained.Id);
        Assert.True(retained.IsDeleted);
        Assert.Equal(deletedVersion, retained.AuthenticationVersion);
        Assert.Empty(await check.Db.Users.ToListAsync());
        Assert.Empty(await check.Db.UserRoles.ToListAsync());
        Assert.Equal(0, await check.Db.UserRefreshTokens.CountAsync(token => token.UserId == deleted.Id));
    }

    [PostgresMfaFact]
    public async Task ConcurrentFirstGoogleSignInsReturnOneControlledFailureAndOneCompleteAccountAsync()
    {
        var barrier = new PauseBeforeGoogleInsert();
        using var harness = new MfaHarness(Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!, interceptor: barrier);
        string tenantId = Guid.NewGuid().ToString();
        using MfaScope setup = harness.Scope(tenantId);
        await setup.Db.Database.MigrateAsync();
        ApplicationRole basic = await AddBasicRoleAsync(setup);
        using MfaScope first = harness.Scope(tenantId);
        using MfaScope second = harness.Scope(tenantId);
        using var transport = new GoogleTransport();
        using var client = new HttpClient(transport);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        barrier.Enabled = true;
        Task<Response<LoginResponse>> one = Handler(first, client).AuthenticateCodeAsync("first-code", new string('v', 43), deadline.Token);
        Task<Response<LoginResponse>> two = Handler(second, client).AuthenticateCodeAsync("second-code", new string('w', 43), deadline.Token);
        Response<LoginResponse>[] results;
        try
        {
            // Both Identity duplicate checks have completed before either INSERT executes.
            await barrier.BothWaiting.Task.WaitAsync(deadline.Token);
            barrier.Resume.TrySetResult();
            results = await Task.WhenAll(one, two).WaitAsync(deadline.Token);
        }
        finally
        {
            barrier.Resume.TrySetResult();
        }

        Response<LoginResponse> accepted = Assert.Single(results, result => !result.IsError);
        LoginResponse completed = Assert.IsType<LoginResponse>(accepted.Data);
        Assert.True(completed.IsCompleted);
        Response<LoginResponse> rejected = Assert.Single(results, result => result.IsError);
        Assert.Equal(401, rejected.StatusCode);
        Assert.Null(rejected.Data);
        Assert.Null(first.Db.Database.CurrentTransaction);
        Assert.Null(second.Db.Database.CurrentTransaction);
        Assert.False(first.Db.ChangeTracker.HasChanges());
        Assert.False(second.Db.ChangeTracker.HasChanges());
        await first.Db.SaveChangesAsync();
        await second.Db.SaveChangesAsync();

        using MfaScope check = harness.Scope(tenantId);
        ApplicationUser created = Assert.Single(await check.Db.Users.ToListAsync());
        Assert.Equal(Email, created.Email);
        ApplicationUserRole membership = Assert.Single(await check.Db.UserRoles.ToListAsync());
        Assert.Equal(created.Id, membership.UserId);
        Assert.Equal(basic.Id, membership.RoleId);
        Assert.Equal(completed.Tokens!.RefreshToken,
            (await check.Db.UserRefreshTokens.SingleAsync(token => token.UserId == created.Id)).RefreshToken);
    }

    private static async Task<ApplicationRole> AddBasicRoleAsync(MfaScope scope)
    {
        var role = new ApplicationRole { Id = Guid.NewGuid().ToString(), Name = RoleConstants.Basic };
        Assert.True((await scope.Services.GetRequiredService<RoleManager<ApplicationRole>>().CreateAsync(role)).Succeeded);
        return role;
    }

    private static ExternalAuth.Handler Handler(MfaScope scope, HttpClient client) => new(scope.Mfa, scope.Db, scope.Users,
        scope.Services.GetRequiredService<RoleManager<ApplicationRole>>(), new ExternalAuth.GoogleAuthClient(client,
            Configuration(), NullLogger<ExternalAuth.GoogleAuthClient>.Instance));

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Urls:RootUiUrl"] = "https://app.example.test",
        ["Authentication:Google:ClientId"] = "synthetic-client",
        ["Authentication:Google:ClientSecret"] = "synthetic-secret"
    }).Build();

    private sealed class GoogleTransport : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = request.RequestUri!.Host == "oauth2.googleapis.com"
                    ? JsonContent.Create(new { access_token = "synthetic-access-token" })
                    : JsonContent.Create(new { email = Email, verified_email = true, given_name = "Google", family_name = "Test" })
            });
    }

    private sealed class PauseBeforeGoogleInsert : SaveChangesInterceptor
    {
        private int waiting;
        public bool Enabled { get; set; }
        public TaskCompletionSource BothWaiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context is ApplicationDbContext db && db.ChangeTracker.Entries<ApplicationUser>()
                    .Any(entry => entry.State == EntityState.Added && entry.Entity.Email == Email))
            {
                if (Interlocked.Increment(ref waiting) == 2) BothWaiting.TrySetResult();
                await Resume.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class ProvisioningAuthenticator(ExternalAuth.Handler handler) : IGoogleAccountAuthenticator
    {
        public Response<LoginResponse>? Result { get; private set; }
        public async Task<Response<LoginResponse>> AuthenticateAsync(GoogleLoginState state, string code, CancellationToken cancellationToken)
        {
            Result = await handler.AuthenticateCodeAsync(code, state.CodeVerifier, cancellationToken);
            return Result;
        }
    }

    private sealed class Registry(string tenantId) : ITenantFinderService
    {
        public Task<Response<Tenant>> Find(string? identifier, CancellationToken cancellationToken = default) =>
            Task.FromResult(Response<Tenant>.Success(new()
            { Id = tenantId, Identifier = "blue", IsActive = true, ValidUpto = DateTime.MaxValue }));
    }

    private sealed class AuthorizationState(GoogleLoginState state) : IGoogleLoginTransactionStore
    {
        public Task<string> CreateAsync<T>(string purpose, string tenantId, string proofHash, T payload,
            DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A rejected account must not produce a credential handoff.");

        public Task<T?> TakeAsync<T>(string id, string purpose, string proof, string? tenantId,
            CancellationToken cancellationToken) where T : class => Task.FromResult(state as T);
    }
}
