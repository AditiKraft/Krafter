using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class AuthTokenServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ConcurrentRefreshWaitsForStorageAndUsesTheSavedToken()
    {
        var saving = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var storage = new TokenStorage(TokenTestData.Create(expired: true))
        {
            BeforeSave = async () =>
            {
                saving.SetResult();
                await allowSave.Task.WaitAsync(Timeout);
            }
        };
        TokenResponse refreshed = TokenTestData.Create();
        var api = new AuthApi { Refresh = _ => Task.FromResult(Response<TokenResponse>.Success(refreshed)) };
        using var coordinator = new TokenRefreshCoordinator();
        var service = new AuthTokenService(api, storage, coordinator);

        Task<bool> first = service.RefreshAsync();
        await saving.Task.WaitAsync(Timeout);
        Task<bool> second = service.RefreshAsync();
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        allowSave.SetResult();

        Assert.All(await Task.WhenAll(first, second).WaitAsync(Timeout), result => Assert.True(result));
        Assert.Equal(1, api.RefreshCount);
        Assert.Equal(refreshed, storage.Tokens);
    }

    [Fact]
    public async Task SeparateUsersCanRefreshAtTheSameTime()
    {
        var allowRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstApi = new AuthApi
        {
            Refresh = async _ =>
            {
                await allowRefresh.Task.WaitAsync(Timeout);
                return Response<TokenResponse>.Success(TokenTestData.Create("alice"));
            }
        };
        var secondApi = new AuthApi
        {
            Refresh = _ => Task.FromResult(Response<TokenResponse>.Success(TokenTestData.Create("bob")))
        };
        using var firstCoordinator = new TokenRefreshCoordinator();
        var first = new AuthTokenService(firstApi, new TokenStorage(TokenTestData.Create("alice", expired: true)), firstCoordinator);
        using var secondCoordinator = new TokenRefreshCoordinator();
        var second = new AuthTokenService(secondApi, new TokenStorage(TokenTestData.Create("bob", expired: true)), secondCoordinator);

        Task<bool> firstRefresh = first.RefreshAsync();
        try
        {
            Assert.True(await second.RefreshAsync().WaitAsync(Timeout));
            Assert.False(firstRefresh.IsCompleted);
            Assert.Equal(1, secondApi.RefreshCount);
        }
        finally
        {
            allowRefresh.SetResult();
            await firstRefresh.WaitAsync(Timeout);
        }
    }

    [Fact]
    public async Task ChangingUsersDoesNotSuppressTheNextRefresh()
    {
        var storage = new TokenStorage(TokenTestData.Create("alice", expired: true));
        var requests = new List<RefreshTokenRequest>();
        var api = new AuthApi
        {
            Refresh = request =>
            {
                requests.Add(request);
                return Task.FromResult(Response<TokenResponse>.Success(TokenTestData.Create()));
            }
        };
        using var coordinator = new TokenRefreshCoordinator();
        var service = new AuthTokenService(api, storage, coordinator);
        Assert.True(await service.RefreshAsync());

        TokenResponse secondUser = TokenTestData.Create("bob", expired: true);
        storage.Tokens = secondUser;
        Assert.True(await service.RefreshAsync());

        Assert.Equal(2, api.RefreshCount);
        Assert.Equal(secondUser.Token, requests[1].Token);
        Assert.Equal(secondUser.RefreshToken, requests[1].RefreshToken);
    }

    [Fact]
    public async Task FailedResponseDoesNotSaveTokensAndAllowsRetry()
    {
        TokenResponse original = TokenTestData.Create(expired: true);
        var storage = new TokenStorage(original);
        var api = new AuthApi
        {
            Refresh = _ => Task.FromResult(Response<TokenResponse>.Unauthorized("Refresh rejected"))
        };
        using var coordinator = new TokenRefreshCoordinator();
        var service = new AuthTokenService(api, storage, coordinator);

        Assert.False(await service.RefreshAsync());
        Assert.Equal(original, storage.Tokens);

        api.Refresh = _ => Task.FromResult(Response<TokenResponse>.Success(TokenTestData.Create()));
        Assert.True(await service.RefreshAsync());
        Assert.Equal(2, api.RefreshCount);
    }

    [Fact]
    public async Task RefreshExceptionDoesNotPreventRetry()
    {
        var storage = new TokenStorage(TokenTestData.Create(expired: true));
        var api = new AuthApi { Refresh = _ => throw new HttpRequestException("Connection failed") };
        using var coordinator = new TokenRefreshCoordinator();
        var service = new AuthTokenService(api, storage, coordinator);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.RefreshAsync());

        api.Refresh = _ => Task.FromResult(Response<TokenResponse>.Success(TokenTestData.Create()));
        Assert.True(await service.RefreshAsync().WaitAsync(Timeout));
    }

    [Fact]
    public async Task CancellingAWaitingCallerDoesNotBlockLaterCalls()
    {
        var allowRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new AuthApi
        {
            Refresh = async _ =>
            {
                await allowRefresh.Task.WaitAsync(Timeout);
                return Response<TokenResponse>.Success(TokenTestData.Create());
            }
        };
        using var coordinator = new TokenRefreshCoordinator();
        var service = new AuthTokenService(api, new TokenStorage(TokenTestData.Create(expired: true)), coordinator);
        using var cancellation = new CancellationTokenSource();
        Task<bool> first = service.RefreshAsync();
        Task<bool> cancelled = service.RefreshAsync(cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        allowRefresh.SetResult();
        Assert.True(await first.WaitAsync(Timeout));
        Assert.True(await service.RefreshAsync().WaitAsync(Timeout));
        Assert.Equal(1, api.RefreshCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-token")]
    public void MissingOrMalformedTokensNeedRefresh(string? token)
    {
        Assert.True(AuthTokenService.NeedsRefresh(token));
    }

    [Fact]
    public async Task ValidTokenDoesNotCallTheRefreshEndpoint()
    {
        var api = new AuthApi();
        using var coordinator = new TokenRefreshCoordinator();
        var service = new AuthTokenService(api, new TokenStorage(TokenTestData.Create()), coordinator);

        Assert.True(await service.RefreshAsync());
        Assert.Equal(0, api.RefreshCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingCredentialsCannotRefresh(bool missingAccessToken)
    {
        TokenResponse tokens = TokenTestData.Create(expired: true);
        tokens = missingAccessToken ? tokens with { Token = "" } : tokens with { RefreshToken = "" };
        var api = new AuthApi();
        using var coordinator = new TokenRefreshCoordinator();
        var service = new AuthTokenService(api, new TokenStorage(tokens), coordinator);

        Assert.False(await service.RefreshAsync());
        Assert.Equal(0, api.RefreshCount);
    }

    [Fact]
    public async Task SynchronizingAnExpiredTokenDoesNotCountAsARefresh()
    {
        TokenResponse expired = TokenTestData.Create(expired: true);
        TokenResponse refreshed = TokenTestData.Create();
        var storage = new TokenStorage(null);
        var api = new AuthApi
        {
            Current = () => Task.FromResult(Response<TokenResponse>.Success(expired)),
            Refresh = request =>
            {
                Assert.Equal(expired.RefreshToken, request.RefreshToken);
                return Task.FromResult(Response<TokenResponse>.Success(refreshed));
            }
        };
        using var coordinator = new TokenRefreshCoordinator();
        var service = new AuthTokenService(api, storage, coordinator);

        Assert.True(await service.SynchronizeFromServerAsync());
        Assert.True(await service.RefreshAsync());
        Assert.Equal(1, api.RefreshCount);
        Assert.Equal(refreshed, storage.Tokens);
    }

    private sealed class TokenStorage(TokenResponse? tokens) : IAuthStorageService
    {
        public TokenResponse? Tokens { get; set; } = tokens;
        public Func<Task>? BeforeSave { get; init; }

        public async ValueTask CacheAuthTokens(TokenResponse tokenResponse)
        {
            if (BeforeSave is not null)
            {
                await BeforeSave();
            }

            Tokens = tokenResponse;
        }

        public Task ClearCacheAsync()
        {
            Tokens = null;
            return Task.CompletedTask;
        }

        public ValueTask<string?> GetCachedAuthTokenAsync() => ValueTask.FromResult(Tokens?.Token);
        public ValueTask<string?> GetCachedRefreshTokenAsync() => ValueTask.FromResult(Tokens?.RefreshToken);
        public ValueTask<DateTime> GetAuthTokenExpiryDate() => ValueTask.FromResult(Tokens?.TokenExpiryTime ?? default);
        public ValueTask<DateTime> GetRefreshTokenExpiryDate() => ValueTask.FromResult(Tokens?.RefreshTokenExpiryTime ?? default);
        public ValueTask<ICollection<string>?> GetCachedPermissionsAsync() => ValueTask.FromResult<ICollection<string>?>(Tokens?.Permissions);
    }

    private sealed class AuthApi : IAuthApiService
    {
        public int RefreshCount { get; private set; }
        public Func<RefreshTokenRequest, Task<Response<TokenResponse>>> Refresh { get; set; } = _ => throw new NotSupportedException();
        public Func<Task<Response<TokenResponse>>> Current { get; init; } = () => throw new NotSupportedException();

        public Task<Response<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellation)
        {
            RefreshCount++;
            return Refresh(request);
        }

        public Task<Response<TokenResponse>> GetCurrentTokenAsync(CancellationToken cancellation) => Current();
        public Task<Response<TokenResponse>> CreateTokenAsync(TokenRequest request, CancellationToken cancellation) => throw new NotSupportedException();
        public Task<Response<TokenResponse>> ExternalAuthAsync(TokenRequest request, CancellationToken cancellation) => throw new NotSupportedException();
        public Task LogoutAsync(CancellationToken cancellation) => throw new NotSupportedException();
    }
}
