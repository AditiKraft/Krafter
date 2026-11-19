using System.Net;
using System.Net.Http.Json;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class GoogleOAuthSplitTransportTests
{
    [Fact]
    public async Task BrowserHandoffJsonSurvivesTheActualSplitMapperServerServiceAndRefitTransport()
    {
        using var recording = new RecordingBackend();
        using var backend = new HttpClient(recording) { BaseAddress = new Uri("https://backend.example.test") };
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthorization();
        builder.Services.AddHybridCache();
        builder.Services.AddSingleton(RestService.For<IAuthApi>(backend));
        builder.Services.AddSingleton<IAuthStorageService, UnusedStorage>();
        builder.Services.AddScoped<IAuthApiService, ServerAuthApiService>();
        await using WebApplication app = builder.Build();
        app.MapAuthTokenBffEndpoints();
        await app.StartAsync();
        try
        {
            string address = Assert.Single(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses);
            using var browser = new HttpClient { BaseAddress = new Uri(address) };
            var request = new GoogleAuthRequest { Handoff = new string('A', 64), BrowserNonce = new string('B', 64) };
            using HttpResponseMessage response = await browser.PostAsJsonAsync("/api/external-auth/google", request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(recording.Received);
            Assert.Equal(request.Handoff, recording.Received.Handoff);
            Assert.Equal(request.BrowserNonce, recording.Received.BrowserNonce);
            Assert.Equal("/api/external-auth/google", recording.Path);
            Response<LoginResponse>? result = await response.Content.ReadFromJsonAsync<Response<LoginResponse>>();
            Assert.True(result!.Data!.RequiresTwoFactor);
            Assert.Null(result.Data.Tokens);
        }
        finally { await app.StopAsync(); }
    }

    private sealed class RecordingBackend : HttpMessageHandler
    {
        public GoogleAuthRequest? Received { get; private set; }
        public string? Path { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath;
            Received = await request.Content!.ReadFromJsonAsync<GoogleAuthRequest>(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(Response<LoginResponse>.Success(LoginResponse.Pending("local-mfa", DateTimeOffset.UtcNow.AddMinutes(5)))) };
        }
    }

    private sealed class UnusedStorage : IAuthStorageService
    {
        public Task ClearCacheAsync() => throw new NotSupportedException();
        public ValueTask<string?> GetCachedAuthTokenAsync() => throw new NotSupportedException();
        public ValueTask<string?> GetCachedRefreshTokenAsync() => throw new NotSupportedException();
        public ValueTask<ICollection<string>?> GetCachedPermissionsAsync() => throw new NotSupportedException();
        public ValueTask CacheAuthTokens(TokenResponse tokenResponse) => throw new NotSupportedException();
        public ValueTask<DateTime> GetAuthTokenExpiryDate() => throw new NotSupportedException();
        public ValueTask<DateTime> GetRefreshTokenExpiryDate() => throw new NotSupportedException();
    }
}
