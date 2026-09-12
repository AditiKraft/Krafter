using System.Net.Http.Json;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Configuration;

public sealed class UiUrlConfigurationTests
{
    [Theory]
    [InlineData(null, null, null, "https://api.example.com/")]
    [InlineData(null, "https://localhost:5199", null, "https://localhost:5199/")]
    [InlineData(null, null, "http://api:8080", "http://api:8080/")]
    [InlineData("http://internal-api:8080", "https://localhost:5199", null, "http://internal-api:8080/")]
    public void SplitHostResolvesServerAddressWithoutChangingPublicAddress(
        string? serverUrl, string? discoveryHttps, string? discoveryHttp, string expected)
    {
        IConfiguration configuration = CreateConfiguration(new()
        {
            ["Urls:ApiBaseUrl"] = "https://api.example.com",
            ["Urls:ServerApiBaseUrl"] = serverUrl,
            ["services:api:https:0"] = discoveryHttps,
            ["services:api:http:0"] = discoveryHttp
        });

        AppUrls urls = UiUrlConfiguration.Resolve(configuration, BlazorHostingMode.SplitHost);

        Assert.Equal(expected, urls.GetServerApiUri().AbsoluteUri);
        Assert.Equal("https://api.example.com", urls.ApiBaseUrl);
    }

    [Theory]
    [InlineData(null, "https://app.example.com/")]
    [InlineData("http://localhost:8080", "http://localhost:8080/")]
    public void SingleHostSupportsStandaloneAndInternalServerAddresses(string? serverUrl, string expected)
    {
        AppUrls urls = UiUrlConfiguration.Resolve(CreateConfiguration(new()
        {
            ["Urls:ServerApiBaseUrl"] = serverUrl
        }), BlazorHostingMode.SingleHost);

        Assert.Null(urls.GetApiUri());
        Assert.Equal(expected, urls.GetServerApiUri().AbsoluteUri);
    }

    [Fact]
    public void SplitHostRequiresPublicApiEvenWhenDiscoveryIsAvailable()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => UiUrlConfiguration.Resolve(
            CreateConfiguration(new() { ["services:api:https:0"] = "https://localhost:5199" }),
            BlazorHostingMode.SplitHost));

        Assert.Contains("Urls:ApiBaseUrl", exception.Message);
    }

    [Fact]
    public void SingleHostRejectsSeparatePublicApiSetting()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => UiUrlConfiguration.Resolve(
            CreateConfiguration(new() { ["Urls:ApiBaseUrl"] = "https://api.example.com" }),
            BlazorHostingMode.SingleHost));

        Assert.Contains("single-host", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("app.example.com")]
    [InlineData("ftp://app.example.com")]
    [InlineData("https://user:password@app.example.com")]
    [InlineData("https://app.example.com/path")]
    [InlineData("https://app.example.com/?key=value")]
    [InlineData("https://app.example.com/#fragment")]
    public void InvalidOriginsProduceActionableConfigurationErrors(string? value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => AppUrls.ParseOrigin(value, "Urls:RootUiUrl"));

        Assert.Contains("Urls:RootUiUrl", exception.Message);
        Assert.DoesNotContain("password", exception.Message);
    }

    [Theory]
    [InlineData("https://app.example.com", "https://app.example.com/google-callback")]
    [InlineData("https://localhost:7291/", "https://localhost:7291/google-callback")]
    [InlineData("http://127.0.0.1:5116", "http://127.0.0.1:5116/google-callback")]
    public void GoogleCallbackUsesConfiguredPublicOrigin(string rootUrl, string expected)
    {
        Assert.Equal(expected, new AppUrls { RootUiUrl = rootUrl }.GetGoogleRedirectUri().AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://api.example.com")]
    public async Task BrowserLoadsPublicUrlsFromHostWithoutInternalAddress(string? publicApiUrl)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(new AppUrls
        {
            RootUiUrl = "https://app.example.com",
            ApiBaseUrl = publicApiUrl,
            ServerApiBaseUrl = "http://private-api:8080"
        });
        await using WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapUiUrlConfiguration();
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(app.Urls)) };
        using HttpResponseMessage response = await client.GetAsync(AppUrls.ConfigurationPath);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync();
        AppUrls? urls = await response.Content.ReadFromJsonAsync<AppUrls>();

        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("private-api", json);
        Assert.DoesNotContain("serverApiBaseUrl", json);
        Assert.NotNull(urls);
        Assert.Equal("https://app.example.com", urls.RootUiUrl);
        Assert.Equal(publicApiUrl, urls.ApiBaseUrl);
        Assert.Null(urls.ServerApiBaseUrl);
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        values["Urls:RootUiUrl"] = "https://app.example.com";
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
