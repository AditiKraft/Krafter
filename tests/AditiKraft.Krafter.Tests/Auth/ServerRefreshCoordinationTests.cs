using System.Collections.Concurrent;
using System.Net;
using System.Text;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Common.Constants;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using AditiKraft.Krafter.UI.Web.Client.Features.Roles;
using AditiKraft.Krafter.UI.Web.Client.Features.Users;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class ServerRefreshCoordinationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ServerFeatureClientsAndRootScopeShareRefreshAndSaveTokensInEveryRequest()
    {
        TokenResponse original = TokenTestData.Create(expired: true);
        TokenResponse fresh = TokenTestData.Create();
        var release = NewSignal();
        var api = new AuthApi(async (_, cancellation) =>
        {
            await release.Task.WaitAsync(Timeout, cancellation);
            return Response<TokenResponse>.Success(fresh);
        });
        var observations = new RefreshObservations();
        var sentTokens = new ConcurrentBag<string?>();
        await using ServiceProvider provider = CreateProvider(api, observations, sentTokens);

        Task<HttpContext> users = RunRequestAsync(provider, original, services =>
            services.GetRequiredService<IUsersApi>().GetUsersAsync(new GetRequestInput()));
        await api.Started.Task.WaitAsync(Timeout);
        Task<HttpContext> roles = RunRequestAsync(provider, original, services =>
            services.GetRequiredService<IRolesApi>().GetRolesAsync(new GetRequestInput()));
        AuthTokenService? rootTokens = null;
        Task<HttpContext> root = RunRequestAsync(provider, original, async services =>
        {
            rootTokens = services.GetRequiredService<AuthTokenService>();
            Assert.True(await rootTokens.RefreshAsync());
        });

        try
        {
            Assert.Equal(2, observations.Services.Distinct().Count());
            Assert.DoesNotContain(rootTokens!, observations.Services);
            Assert.False(root.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
        }

        HttpContext[] contexts = await Task.WhenAll(users, roles, root).WaitAsync(Timeout);
        Assert.Equal(1, api.RefreshCount);
        Assert.Equal(2, sentTokens.Count);
        Assert.All(sentTokens, token => Assert.Equal(fresh.Token, token));
        Assert.All(contexts, context =>
        {
            Assert.Equal(fresh.Token, context.Items[StorageConstants.Local.AuthToken]);
            Assert.Equal(fresh.RefreshToken, context.Items[StorageConstants.Local.RefreshToken]);
            Assert.Contains(context.Response.Headers.SetCookie,
                cookie => cookie!.StartsWith($"{StorageConstants.Local.RefreshToken}={fresh.RefreshToken};", StringComparison.Ordinal));
        });

        // A later request can still carry the old cookies while the first response is in flight.
        HttpContext late = await RunRequestAsync(provider, original, async services =>
            Assert.True(await services.GetRequiredService<AuthTokenService>().RefreshAsync()));
        Assert.Equal(1, api.RefreshCount);
        Assert.Equal(fresh.RefreshToken, late.Items[StorageConstants.Local.RefreshToken]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DifferentTokenPairsRefreshIndependently(bool sameAccessToken)
    {
        TokenResponse alice = TokenTestData.Create(expired: true);
        TokenResponse bob = sameAccessToken
            ? alice with { RefreshToken = "another-session-refresh-token" }
            : TokenTestData.Create("bob", expired: true);
        TokenResponse freshAlice = TokenTestData.Create();
        TokenResponse freshBob = TokenTestData.Create("bob");
        var releaseAlice = NewSignal();
        var api = new AuthApi(async (request, cancellation) =>
        {
            if (request.RefreshToken == alice.RefreshToken)
            {
                await releaseAlice.Task.WaitAsync(Timeout, cancellation);
                return Response<TokenResponse>.Success(freshAlice);
            }

            return Response<TokenResponse>.Success(freshBob);
        });
        await using ServiceProvider provider = CreateProvider(api);
        using IServiceScope firstScope = provider.CreateScope();
        using IServiceScope secondScope = provider.CreateScope();
        Task<Response<TokenResponse>> first = RefreshAsync(firstScope, alice);
        await api.Started.Task.WaitAsync(Timeout);

        try
        {
            Response<TokenResponse> second = await RefreshAsync(secondScope, bob).WaitAsync(Timeout);
            AssertTokensEqual(freshBob, second.Data);
            Assert.False(first.IsCompleted);
        }
        finally
        {
            releaseAlice.TrySetResult();
        }

        AssertTokensEqual(freshAlice, (await first.WaitAsync(Timeout)).Data);
        Assert.Equal(2, api.RefreshCount);
    }

    [Theory]
    [InlineData("error")]
    [InlineData("missing-data")]
    [InlineData("blank-token")]
    public async Task FailedOrIncompleteResponsesCanBeRetriedImmediately(string failure)
    {
        TokenResponse original = TokenTestData.Create(expired: true);
        TokenResponse fresh = TokenTestData.Create();
        int calls = 0;
        var api = new AuthApi((_, _) => Task.FromResult(Interlocked.Increment(ref calls) > 1
            ? Response<TokenResponse>.Success(fresh)
            : failure switch
            {
                "error" => new Response<TokenResponse> { IsError = true, StatusCode = 503 },
                "missing-data" => new Response<TokenResponse> { StatusCode = 200 },
                _ => Response<TokenResponse>.Success(fresh with { RefreshToken = " " })
            }));
        await using ServiceProvider provider = CreateProvider(api);
        using IServiceScope firstScope = provider.CreateScope();
        using IServiceScope secondScope = provider.CreateScope();

        await RefreshAsync(firstScope, original);
        Response<TokenResponse> retried = await RefreshAsync(secondScope, original);

        Assert.False(retried.IsError);
        AssertTokensEqual(fresh, retried.Data);
        Assert.Equal(2, api.RefreshCount);
    }

    [Fact]
    public async Task CancelingFirstCallerDoesNotCancelAnotherCallerWaitingForTheSameRefresh()
    {
        TokenResponse original = TokenTestData.Create(expired: true);
        TokenResponse fresh = TokenTestData.Create();
        var release = NewSignal();
        CancellationToken refreshCancellation = default;
        var api = new AuthApi(async (_, cancellation) =>
        {
            refreshCancellation = cancellation;
            await release.Task.WaitAsync(Timeout, cancellation);
            return Response<TokenResponse>.Success(fresh);
        });
        await using ServiceProvider provider = CreateProvider(api);
        using IServiceScope firstScope = provider.CreateScope();
        using IServiceScope secondScope = provider.CreateScope();
        using var cancellationSource = new CancellationTokenSource();
        Task<Response<TokenResponse>> first = RefreshAsync(firstScope, original, cancellationSource.Token);
        await api.Started.Task.WaitAsync(Timeout);
        Task<Response<TokenResponse>> second = RefreshAsync(secondScope, original);

        try
        {
            cancellationSource.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Timeout));
            Assert.False(refreshCancellation.IsCancellationRequested);
            Assert.False(second.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
        }

        AssertTokensEqual(fresh, (await second.WaitAsync(Timeout)).Data);
        Assert.Equal(1, api.RefreshCount);
    }

    private static ServiceProvider CreateProvider(AuthApi api, RefreshObservations? observations = null,
        ConcurrentBag<string?>? sentTokens = null)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Urls:RootUiUrl"] = "https://localhost:7291",
                ["Urls:ApiBaseUrl"] = "https://localhost:5001"
            }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddUiHostServices(configuration);
        services.RemoveAll<IDistributedCache>();
        services.AddDistributedMemoryCache();
        services.AddSingleton<IAuthApi>(api);
        if (observations is not null)
        {
            services.AddSingleton(observations);
            services.AddScoped<IAuthenticationService, ForwardingAuthenticationService>();
        }

        if (sentTokens is not null)
        {
            services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(
                () => new RecordingTransport(sentTokens)));
        }

        return services.BuildServiceProvider();
    }

    private static async Task<HttpContext> RunRequestAsync(ServiceProvider provider, TokenResponse tokens,
        Func<IServiceProvider, Task> action)
    {
        using IServiceScope scope = provider.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost", 5002);
        context.Request.Headers.Cookie = string.Join("; ",
            $"{StorageConstants.Local.AuthToken}={tokens.Token}",
            $"{StorageConstants.Local.RefreshToken}={tokens.RefreshToken}");
        IHttpContextAccessor accessor = provider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = context;
        try
        {
            await action(scope.ServiceProvider);
            return context;
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    private static Task<Response<TokenResponse>> RefreshAsync(IServiceScope scope, TokenResponse tokens,
        CancellationToken cancellationToken = default) =>
        scope.ServiceProvider.GetRequiredService<IAuthApiService>().RefreshTokenAsync(
            new RefreshTokenRequest { Token = tokens.Token, RefreshToken = tokens.RefreshToken }, cancellationToken);

    private static void AssertTokensEqual(TokenResponse expected, TokenResponse? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Token, actual.Token);
        Assert.Equal(expected.RefreshToken, actual.RefreshToken);
        Assert.Equal(expected.TokenExpiryTime, actual.TokenExpiryTime);
        Assert.Equal(expected.RefreshTokenExpiryTime, actual.RefreshTokenExpiryTime);
        Assert.Equal(expected.Permissions, actual.Permissions);
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class RefreshObservations
    {
        public ConcurrentBag<AuthTokenService> Services { get; } = [];
    }

    private sealed class ForwardingAuthenticationService(AuthTokenService tokens, RefreshObservations observations)
        : IAuthenticationService
    {
        public event Action<string?>? LoginChange { add { } remove { } }

        public Task<bool> RefreshAsync()
        {
            observations.Services.Add(tokens);
            return tokens.RefreshAsync();
        }

        public Task<bool> LoginAsync(TokenRequest model) => throw new NotSupportedException();
        public Task LogoutAsync(string methodName) => throw new NotSupportedException();
    }

    private sealed class RecordingTransport(ConcurrentBag<string?> sentTokens) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            sentTokens.Add(request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"isError\":false,\"statusCode\":200}", Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class AuthApi(Func<RefreshTokenRequest, CancellationToken, Task<Response<TokenResponse>>> refresh)
        : IAuthApi
    {
        private int _refreshCount;
        public int RefreshCount => _refreshCount;
        public TaskCompletionSource Started { get; } = NewSignal();

        public Task<Response<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _refreshCount);
            Started.TrySetResult();
            return refresh(request, cancellationToken);
        }

        public Task<Response<TokenResponse>> CreateTokenAsync(TokenRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Response<TokenResponse>> GoogleAuthAsync(GoogleAuthRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task LogoutAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
