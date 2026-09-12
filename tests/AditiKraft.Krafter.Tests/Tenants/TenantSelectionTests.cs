using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence.Tenants;
using AditiKraft.Krafter.Backend.Web.Middleware;
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
        Assert.Equal($"https://{host}", tenant.Tenant.TenantLink);
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
