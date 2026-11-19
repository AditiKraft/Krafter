using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class TwoFactorChallengeResponseTests
{
    [Theory]
    [InlineData(BlazorHostingMode.SplitHost, "/api/two-factor/status", 401)]
    [InlineData(BlazorHostingMode.SingleHost, "/api/two-factor/status", 401)]
    [InlineData(BlazorHostingMode.SplitHost, "/users", 302)]
    [InlineData(BlazorHostingMode.SingleHost, "/users", 302)]
    public async Task ApiClientsGetJsonInsteadOfAnHtmlLoginPage(BlazorHostingMode mode, string path, int status)
    {
        using ServiceProvider services = new ServiceCollection().AddOptions().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Path = path;
        http.Response.Body = new MemoryStream();
        var scheme = new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler));
        var context = new JwtBearerChallengeContext(http, scheme, new JwtBearerOptions(), new AuthenticationProperties());
        await new BlazorJwtBearerEvents(mode).Challenge(context);
        Assert.Equal(status, http.Response.StatusCode);
        if (status == 401) Assert.StartsWith("application/json", http.Response.ContentType);
        else Assert.StartsWith("/login?ReturnUrl=", http.Response.Headers.Location.ToString());
    }
}
