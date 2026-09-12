using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence.Tenants;
using AditiKraft.Krafter.Backend.Web.Middleware;
using AditiKraft.Krafter.Contracts.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AditiKraft.Krafter.Tests.Tenants;

public sealed class TenantSelectionTests
{
    [Theory]
    [InlineData("127.0.0.1:5001", "blue", "blue")]
    [InlineData("127.0.0.1:5001", null, "root")]
    [InlineData("[::1]:5001", "blue", "blue")]
    [InlineData("[::ffff:127.0.0.1]:5001", "blue", "blue")]
    [InlineData("[::1]:5001", null, "root")]
    [InlineData("localhost:5001", "blue", "blue")]
    [InlineData("example.com", "blue", "blue")]
    [InlineData("blue.example.com:5001", "127", "blue")]
    public async Task IpHostsUseTenantHeaderOrRootWhileDomainHostsKeepSubdomainPriority(
        string host, string? header, string expectedTenant)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new TenantDbContext(options);
        db.Tenants.AddRange(ActiveTenant("blue"), ActiveTenant("127"));
        await db.SaveChangesAsync();
        var tenant = new CurrentTenantService();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Urls:RootUiUrl"] = "https://example.com" }).Build();
        var middleware = new MultiTenantServiceMiddleware(
            new TenantFinderService(db), tenant, new CurrentUser(), configuration);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString(host);
        if (header is not null)
        {
            context.Request.Headers["x-tenant-identifier"] = header;
        }

        bool nextCalled = false;
        await middleware.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        Assert.True(nextCalled);
        Assert.Equal(expectedTenant, tenant.Tenant.Id);
        Assert.Equal(expectedTenant == "root" ? "https://example.com" : $"https://{expectedTenant}.example.com", tenant.Tenant.TenantLink);
    }

    [Theory]
    [InlineData("missing.getkrafter.dev", null, null, "/users", 404, null)]
    [InlineData("api.getkrafter.dev", "missing", null, "/users", 404, null)]
    [InlineData("www.getkrafter.dev", null, null, "/users", 404, null)]
    [InlineData("root.getkrafter.dev", null, null, "/users", 404, null)]
    [InlineData("nested.blue.getkrafter.dev", null, null, "/users", 404, null)]
    [InlineData("krafter.getkrafter.dev", null, null, "/users", 200, "root")]
    [InlineData("blue.getkrafter.dev", "red", null, "/users", 200, "blue")]
    [InlineData("api.getkrafter.dev", "BLUE", null, "/users", 200, "blue")]
    [InlineData("api.getkrafter.dev", null, "blue", "/users", 200, "root")]
    public async Task SiblingHostSelectionDoesNotFallBackForUnknownTenants(
        string host, string? header, string? query, string path, int status, string? identifier)
    {
        await AssertSelectionAsync(host, header, query, path, status, identifier);
    }

    [Fact]
    public async Task RealtimeQueryIsLimitedToHubAndHeaderTakesPriority()
    {
        string path = $"/{ApiRoutes.ApiPrefix}/RealtimeHub";
        await AssertSelectionAsync("api.getkrafter.dev", null, "blue", path, 200, "blue");
        await AssertSelectionAsync("api.getkrafter.dev", null, "blue", path + "/negotiate", 200, "blue");
        await AssertSelectionAsync("api.getkrafter.dev", "root", "blue", path, 200, "root");
        await AssertSelectionAsync("api.getkrafter.dev", null, "missing", path, 404, null);
        await AssertSelectionAsync("api.getkrafter.dev", null, "blue", path + "-other", 200, "root");
    }

    private static async Task AssertSelectionAsync(string host, string? header, string? query,
        string path, int expectedStatus, string? expectedIdentifier)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new TenantDbContext(options);
        db.Tenants.Add(ActiveTenant("blue"));
        await db.SaveChangesAsync();
        var tenant = new CurrentTenantService();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Urls:RootUiUrl"] = "https://krafter.getkrafter.dev",
            ["Urls:TenantBaseDomain"] = "getkrafter.dev",
            ["Urls:ApiBaseUrl"] = "https://api.getkrafter.dev"
        }).Build();
        var middleware = new MultiTenantServiceMiddleware(new TenantFinderService(db), tenant, new CurrentUser(), configuration);
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString(host);
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (header is not null) context.Request.Headers["x-tenant-identifier"] = header;
        if (query is not null) context.Request.QueryString = new QueryString("?tenantIdentifier=" + query);
        bool nextCalled = false;
        await middleware.InvokeAsync(context, _ => { nextCalled = true; return Task.CompletedTask; });
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedStatus == 200, nextCalled);
        if (expectedIdentifier is not null)
        {
            Assert.Equal(expectedIdentifier, tenant.Tenant.Identifier);
            Assert.Equal(expectedIdentifier == "root" ? "https://krafter.getkrafter.dev" : $"https://{expectedIdentifier}.getkrafter.dev",
                tenant.Tenant.TenantLink);
        }
        else
        {
            context.Response.Body.Position = 0;
            Assert.Contains("Tenant not found", await new StreamReader(context.Response.Body).ReadToEndAsync());
        }
    }

    private static Tenant ActiveTenant(string identifier) => new()
    {
        Id = identifier,
        Identifier = identifier,
        AdminEmail = $"admin@{identifier}.example.com",
        IsActive = true,
        ValidUpto = DateTime.MaxValue
    };
}
