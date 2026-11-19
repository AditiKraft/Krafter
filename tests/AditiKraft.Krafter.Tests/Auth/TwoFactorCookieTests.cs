using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class TwoFactorCookieTests
{
    [Theory]
    [InlineData("/api/tokens")]
    [InlineData("/api/external-auth/google")]
    public async Task PendingChallengeDoesNotPersistAuthentication(string path)
    {
        var storage = new RecordingStorage();
        await Invoke(path, Response<LoginResponse>.Success(LoginResponse.Pending("opaque", DateTimeOffset.UtcNow.AddMinutes(5))), storage);
        Assert.Null(storage.Saved);
    }

    [Theory]
    [InlineData("/api/tokens")]
    [InlineData("/api/external-auth/google")]
    [InlineData("/api/two-factor/login")]
    public async Task CompletedLoginPersistsRealTokens(string path)
    {
        var storage = new RecordingStorage();
        TokenResponse tokens = TokenTestData.Create();
        object response = path.EndsWith("two-factor/login", StringComparison.Ordinal)
            ? Response<TokenResponse>.Success(tokens)
            : Response<LoginResponse>.Success(LoginResponse.Completed(tokens));
        await Invoke(path, response, storage);
        Assert.NotNull(storage.Saved);
        Assert.Equal(tokens.Token, storage.Saved.Token);
        Assert.Equal(tokens.RefreshToken, storage.Saved.RefreshToken);
    }

    private static async Task Invoke(string path, object response, RecordingStorage storage)
    {
        var services = new ServiceCollection().AddLogging().AddSingleton<IAuthStorageService>(storage).BuildServiceProvider();
        using (services)
        {
            var context = new DefaultHttpContext { RequestServices = services };
            context.Request.Path = path;
            context.Request.Method = "POST";
            context.Response.Body = new MemoryStream();
            await new AuthCookieMiddleware(async ctx =>
            {
                await JsonSerializer.SerializeAsync(ctx.Response.Body, response, response.GetType());
            }).InvokeAsync(context);
        }
    }

    private sealed class RecordingStorage : IAuthStorageService
    {
        public TokenResponse? Saved { get; private set; }
        public ValueTask CacheAuthTokens(TokenResponse tokenResponse) { Saved = tokenResponse; return ValueTask.CompletedTask; }
        public Task ClearCacheAsync() => Task.CompletedTask;
        public ValueTask<DateTime> GetAuthTokenExpiryDate() => ValueTask.FromResult(default(DateTime));
        public ValueTask<DateTime> GetRefreshTokenExpiryDate() => ValueTask.FromResult(default(DateTime));
        public ValueTask<string?> GetCachedAuthTokenAsync() => ValueTask.FromResult<string?>(null);
        public ValueTask<string?> GetCachedRefreshTokenAsync() => ValueTask.FromResult<string?>(null);
        public ValueTask<ICollection<string>?> GetCachedPermissionsAsync() => ValueTask.FromResult<ICollection<string>?>(null);
    }
}
