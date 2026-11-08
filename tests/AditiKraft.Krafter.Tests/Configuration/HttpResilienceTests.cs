using System.Net;
using AditiKraft.Krafter.Aspire.ServiceDefaults;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.UI.Web.Client.Features.Auth;
using AditiKraft.Krafter.UI.Web.Client.Features.Roles;
using AditiKraft.Krafter.UI.Web.Client.Features.Tenants;
using AditiKraft.Krafter.UI.Web.Client.Features.Users;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Refit;

namespace AditiKraft.Krafter.Tests.Configuration;

// Production registration and handler pipelines, with an in-memory transport only.
public sealed class HttpResilienceTests
{
    [Theory]
    [InlineData("POST", false)]
    [InlineData("PUT", false)]
    [InlineData("PATCH", false)]
    [InlineData("DELETE", false)]
    [InlineData("CONNECT", false)]
    [InlineData("POST", true)]
    [InlineData("PUT", true)]
    [InlineData("PATCH", true)]
    [InlineData("DELETE", true)]
    [InlineData("CONNECT", true)]
    public async Task UnsafeMethodsReachTransportOnceAfterUncertainFailure(string method, bool connectionLost)
    {
        var transport = new SyntheticTransport
        {
            Status = HttpStatusCode.ServiceUnavailable,
            Failure = connectionLost ? new HttpRequestException("Synthetic response loss after commit") : null
        };
        await using WebApplication app = CreateApp(transport);
        using HttpClient client = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("probe");
        using var request = new HttpRequestMessage(new HttpMethod(method), "https://api.example.test/mutation");

        if (connectionLost)
            await Assert.ThrowsAsync<HttpRequestException>(() => client.SendAsync(request));
        else
        {
            using HttpResponseMessage response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        Assert.Equal(1, transport.Calls);
    }

    [Theory]
    [InlineData(BlazorHostingMode.SplitHost)]
    [InlineData(BlazorHostingMode.SingleHost)]
    public async Task OneUseAndMutationRefitClientsDoNotReplayPosts(BlazorHostingMode mode)
    {
        var transport = new SyntheticTransport { Status = HttpStatusCode.ServiceUnavailable };
        await using WebApplication app = CreateApp(transport, mode);
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IAuthApi auth = scope.ServiceProvider.GetRequiredService<IAuthApi>();
        ITwoFactorApi twoFactor = scope.ServiceProvider.GetRequiredService<ITwoFactorApi>();
        IGoogleOAuthApi google = scope.ServiceProvider.GetRequiredService<IGoogleOAuthApi>();
        Func<Task>[] mutations =
        [
            () => auth.CreateTokenAsync(new TokenRequest()),
            () => auth.RefreshTokenAsync(new RefreshTokenRequest()),
            () => auth.CompleteTwoFactorAsync(new TwoFactorRequest()),
            () => auth.GoogleAuthAsync(new GoogleAuthRequest()),
            () => auth.LogoutAsync(),
            () => twoFactor.SetupAsync(new TwoFactorManagementRequest()),
            () => twoFactor.ConfirmAsync(new TwoFactorRequest()),
            () => twoFactor.DisableAsync(new TwoFactorManagementRequest()),
            () => twoFactor.RegenerateAsync(new TwoFactorManagementRequest()),
            () => scope.ServiceProvider.GetRequiredService<IUsersApi>().DeleteUserAsync("synthetic-user"),
            () => scope.ServiceProvider.GetRequiredService<IRolesApi>().DeleteRoleAsync("synthetic-role"),
            () => scope.ServiceProvider.GetRequiredService<ITenantsApi>().DeleteTenantAsync("synthetic-tenant"),
            () => google.StartAsync(new GoogleLoginStartRequest(), CancellationToken.None),
            () => google.CallbackAsync(new GoogleLoginCallbackRequest(), CancellationToken.None)
        ];

        foreach (Func<Task> mutate in mutations)
        {
            int before = transport.Calls;
            ApiException error = await Assert.ThrowsAsync<ApiException>(mutate);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
            Assert.Equal(before + 1, transport.Calls);
        }
    }

    [Theory]
    [InlineData(BlazorHostingMode.SplitHost)]
    [InlineData(BlazorHostingMode.SingleHost)]
    public async Task SafeSessionReadRetriesATransientFailure(BlazorHostingMode mode)
    {
        var transport = new SyntheticTransport { FailFirst = true };
        await using WebApplication app = CreateApp(transport, mode);
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();

        var response = await scope.ServiceProvider.GetRequiredService<IAuthApi>()
            .ValidateSessionAsync("Bearer synthetic-token");

        Assert.True(response.Data);
        Assert.Equal(2, transport.Calls);
    }

    [Theory]
    [InlineData(BlazorHostingMode.SplitHost)]
    [InlineData(BlazorHostingMode.SingleHost)]
    public async Task OneClientsThrottleDoesNotOpenTheSharedAuthCircuit(BlazorHostingMode mode)
    {
        var transport = new SyntheticTransport { Status = HttpStatusCode.TooManyRequests };
        await using WebApplication app = CreateApp(transport, mode);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using (AsyncServiceScope throttledScope = app.Services.CreateAsyncScope())
        {
            IAuthApi auth = throttledScope.ServiceProvider.GetRequiredService<IAuthApi>();
            int throughput = new HttpStandardResilienceOptions().CircuitBreaker.MinimumThroughput;
            for (int attempt = 0; attempt <= throughput; attempt++)
            {
                ApiException error = await Assert.ThrowsAsync<ApiException>(() =>
                    auth.CreateTokenAsync(new TokenRequest(), deadline.Token));
                Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
            }
            Assert.Equal(throughput + 1, transport.Calls);
        }

        transport.Status = HttpStatusCode.OK;
        int before = transport.Calls;
        await using AsyncServiceScope healthyScope = app.Services.CreateAsyncScope();
        var response = await healthyScope.ServiceProvider.GetRequiredService<IAuthApi>()
            .ValidateSessionAsync("Bearer unrelated-client", deadline.Token);
        Assert.True(response.Data);
        Assert.Equal(before + 1, transport.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DependencyFailuresStillOpenTheCircuitAtDefaultThroughput(bool timeout)
    {
        var transport = new SyntheticTransport
        {
            Status = HttpStatusCode.ServiceUnavailable,
            Failure = timeout ? new TimeoutRejectedException("Synthetic dependency timeout") : null
        };
        await using WebApplication app = CreateApp(transport);
        using HttpClient client = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("probe");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        int throughput = new HttpStandardResilienceOptions().CircuitBreaker.MinimumThroughput;
        for (int attempt = 0; attempt < throughput; attempt++)
        {
            if (timeout)
                await Assert.ThrowsAsync<TimeoutRejectedException>(() =>
                    client.PostAsync("https://api.example.test/login", null, deadline.Token));
            else
            {
                using HttpResponseMessage response = await client.PostAsync(
                    "https://api.example.test/login", null, deadline.Token);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            }
        }

        Assert.Equal(throughput, transport.Calls);
        transport.Status = HttpStatusCode.OK;
        transport.Failure = null;
        await Assert.ThrowsAnyAsync<BrokenCircuitException>(() =>
            client.GetAsync("https://api.example.test/healthy", deadline.Token));
        Assert.Equal(throughput, transport.Calls);
    }

    private static WebApplication CreateApp(SyntheticTransport transport,
        BlazorHostingMode mode = BlazorHostingMode.SplitHost)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        { EnvironmentName = Environments.Production });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Urls:RootUiUrl"] = "https://app.example.test",
            ["Urls:ApiBaseUrl"] = mode == BlazorHostingMode.SplitHost ? "https://api.example.test" : null,
            ["ConnectionStrings:appDb"] = "Host=unused.example.test;Database=unused;Username=unused;Password=unused"
        });
        builder.Logging.ClearProviders();
        builder.AddServiceDefaults();
        builder.Services.AddUiHostServices(builder.Configuration, mode);
        builder.Services.AddHttpClient("probe");
        builder.Services.ConfigureHttpClientDefaults(client =>
            client.ConfigurePrimaryHttpMessageHandler(() => transport));
        // Remove only retry waits. Keep predicates, attempt counts, breaker throughput and windows.
        builder.Services.PostConfigureAll<HttpStandardResilienceOptions>(options =>
        {
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.UseJitter = false;
        });
        WebApplication app = builder.Build();
        app.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            Request = { Scheme = "https", Host = new HostString("app.example.test") }
        };
        return app;
    }

    private sealed class SyntheticTransport : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool FailFirst { get; init; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public Exception? Failure { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            if (Failure is not null)
                return Task.FromException<HttpResponseMessage>(Failure);
            return Task.FromResult(new HttpResponseMessage(FailFirst && Calls == 1 ? HttpStatusCode.ServiceUnavailable : Status)
            {
                RequestMessage = request,
                Content = new StringContent("""{"isError":false,"data":true,"statusCode":200}""")
            });
        }
    }
}
