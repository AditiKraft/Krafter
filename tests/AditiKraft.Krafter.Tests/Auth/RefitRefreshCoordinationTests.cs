using System.Collections.Concurrent;
using System.Net;
using System.Text;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client;
using AditiKraft.Krafter.UI.Web.Client.Features.Roles;
using AditiKraft.Krafter.UI.Web.Client.Features.Users;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Http;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Refit;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class RefitRefreshCoordinationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task FeatureClientsAndRootServiceShareBrowserRefreshCoordination()
    {
        TokenResponse freshTokens = TokenTestData.Create();
        var storage = new TokenStorage(TokenTestData.Create(expired: true));
        var api = new BlockingAuthApi(freshTokens);
        var observations = new RefreshObservations();
        var sentTokens = new ConcurrentBag<string?>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new AppUrls
        {
            RootUiUrl = "https://localhost:5002",
            ApiBaseUrl = "https://localhost:5001"
        });
        services.AddSingleton<NavigationManager, BrowserNavigationManager>();
        services.AddSingleton<IFormFactor, FormFactor>();
        services.AddSingleton<IAuthStorageService>(storage);
        services.AddSingleton<IAuthApiService>(api);
        services.AddSingleton(observations);
        services.AddScoped<TenantIdentifier>();
        services.AddUIServices();
        services.AddSingleton<TokenRefreshCoordinator>();
        services.AddScoped<IAuthenticationService, ForwardingAuthenticationService>();
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(
            () => new RecordingTransport(sentTokens)));
        services.AddApiRefitClients();

        await using ServiceProvider provider = services.BuildServiceProvider();
        IUsersApi users = provider.GetRequiredService<IUsersApi>();
        IRolesApi roles = provider.GetRequiredService<IRolesApi>();
        AuthTokenService rootTokens = provider.GetRequiredService<AuthTokenService>();

        Task usersRequest = users.GetUsersAsync(new GetRequestInput());
        await api.Started.Task.WaitAsync(Timeout);
        Task rolesRequest = roles.GetRolesAsync(new GetRequestInput());
        await observations.BothClientsStarted.Task.WaitAsync(Timeout);
        Task<bool> rootRefresh = rootTokens.RefreshAsync();

        try
        {
            Assert.False(rootRefresh.IsCompleted);
            Assert.Equal(2, observations.Services.Distinct().Count());
            Assert.DoesNotContain(rootTokens, observations.Services);
        }
        finally
        {
            api.AllowRefresh.TrySetResult();
        }

        await Task.WhenAll(usersRequest, rolesRequest, rootRefresh).WaitAsync(Timeout);
        Assert.True(await rootRefresh);
        Assert.Equal(1, api.RefreshCount);
        Assert.Equal(2, sentTokens.Count);
        Assert.All(sentTokens, token => Assert.Equal(freshTokens.Token, token));
        Assert.Equal(freshTokens.RefreshToken, storage.Tokens.RefreshToken);
    }

    private sealed class RefreshObservations
    {
        public ConcurrentBag<AuthTokenService> Services { get; } = [];
        public TaskCompletionSource BothClientsStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Record(AuthTokenService service)
        {
            Services.Add(service);
            if (Services.Count == 2)
            {
                BothClientsStarted.TrySetResult();
            }
        }
    }

    private sealed class ForwardingAuthenticationService(
        AuthTokenService tokens,
        RefreshObservations observations) : IAuthenticationService
    {
        public event Action<string?>? LoginChange { add { } remove { } }

        public Task<bool> RefreshAsync()
        {
            observations.Record(tokens);
            return tokens.RefreshAsync();
        }

        public Task<bool> LoginAsync(TokenRequest model) => throw new NotSupportedException();
        public Task LogoutAsync(string methodName) => throw new NotSupportedException();
    }

    private sealed class BrowserNavigationManager : NavigationManager
    {
        public BrowserNavigationManager() => Initialize("https://localhost:5002/", "https://localhost:5002/users");

        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
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

    private sealed class BlockingAuthApi(TokenResponse refreshed) : IAuthApiService
    {
        private int _refreshCount;
        public int RefreshCount => _refreshCount;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowRefresh { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<Response<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellation)
        {
            Interlocked.Increment(ref _refreshCount);
            Started.TrySetResult();
            await AllowRefresh.Task.WaitAsync(Timeout, cancellation);
            return Response<TokenResponse>.Success(refreshed);
        }

        public Task<Response<TokenResponse>> CreateTokenAsync(TokenRequest request, CancellationToken cancellation) => throw new NotSupportedException();
        public Task<Response<TokenResponse>> ExternalAuthAsync(TokenRequest request, CancellationToken cancellation) => throw new NotSupportedException();
        public Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellation) => throw new NotSupportedException();
        public Task LogoutAsync(CancellationToken cancellation) => throw new NotSupportedException();
    }

    private sealed class TokenStorage(TokenResponse tokens) : IAuthStorageService
    {
        public TokenResponse Tokens { get; private set; } = tokens;

        public ValueTask CacheAuthTokens(TokenResponse tokenResponse)
        {
            Tokens = tokenResponse;
            return ValueTask.CompletedTask;
        }

        public ValueTask<string?> GetCachedAuthTokenAsync() => ValueTask.FromResult<string?>(Tokens.Token);
        public ValueTask<string?> GetCachedRefreshTokenAsync() => ValueTask.FromResult<string?>(Tokens.RefreshToken);
        public ValueTask<ICollection<string>?> GetCachedPermissionsAsync() => ValueTask.FromResult<ICollection<string>?>(Tokens.Permissions);
        public ValueTask<DateTime> GetAuthTokenExpiryDate() => ValueTask.FromResult(Tokens.TokenExpiryTime);
        public ValueTask<DateTime> GetRefreshTokenExpiryDate() => ValueTask.FromResult(Tokens.RefreshTokenExpiryTime);
        public Task ClearCacheAsync() => throw new NotSupportedException();
    }
}
