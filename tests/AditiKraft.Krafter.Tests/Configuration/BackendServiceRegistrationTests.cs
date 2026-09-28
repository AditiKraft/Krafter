using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AditiKraft.Krafter.Tests.Configuration;

public sealed class BackendServiceRegistrationTests
{
    [Theory]
    [InlineData(typeof(IUserService), typeof(UserService))]
    [InlineData(typeof(ITokenService), typeof(TokenService))]
    public async Task BackendCompositionResolvesOneServicePerScopeAsync(Type serviceType, Type implementationType)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Urls:RootUiUrl"] = "https://app.example.com",
            ["ConnectionStrings:appDb"] = "Host=localhost;Database=registration-test;Username=test;Password=test",
            ["SMTPEmailSettings:Host"] = "smtp.example.com",
            ["SecuritySettings:JwtSettings:Key"] = "registration-test-signing-key-at-least-32-characters"
        });
        builder.AddBackendServices();
        // Resolve services without starting the host or connecting to its database and background jobs.
        await using WebApplication app = builder.Build();
        await using AsyncServiceScope firstScope = app.Services.CreateAsyncScope();
        await using AsyncServiceScope secondScope = app.Services.CreateAsyncScope();

        object? firstService = Assert.Single(firstScope.ServiceProvider.GetServices(serviceType));
        Assert.IsType(implementationType, firstService);
        Assert.Same(firstService, firstScope.ServiceProvider.GetRequiredService(serviceType));

        object? secondService = Assert.Single(secondScope.ServiceProvider.GetServices(serviceType));
        Assert.IsType(implementationType, secondService);
        Assert.NotSame(firstService, secondService);
    }
}
