using System.Collections.Concurrent;
using System.Security.Claims;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AditiKraft.Krafter.Tests.Authorization;

public sealed class PermissionAuthorizationTests
{
    private const string Permission = "Permissions.Users.View";

    [Theory]
    [InlineData("tenant-a", "alice", true)]
    [InlineData("tenant-a", "root-user", false)]
    [InlineData("tenant-a", "bob", false)]
    [InlineData("root", "root-user", true)]
    public async Task DatabaseCheckUsesTheCurrentTenant(string tenantId, string userId, bool allowed)
    {
        var probes = new ConcurrentBag<PermissionUserService>();
        await using ServiceProvider provider = CreateProvider(probes);
        await using AsyncServiceScope requestScope = provider.CreateAsyncScope();
        SetTenant(requestScope, tenantId);
        var context = CreateContext(userId);

        await requestScope.ServiceProvider.GetRequiredService<IAuthorizationHandler>().HandleAsync(context);

        Assert.Equal(allowed, context.HasSucceeded);
        PermissionUserService probe = Assert.Single(probes);
        Assert.Equal(tenantId, probe.TenantIdAtConstruction);
        Assert.NotSame(requestScope.ServiceProvider.GetRequiredService<ITenantGetterService>(), probe.TenantGetter);
        Assert.True(probe.IsDisposed);
    }

    [Fact]
    public async Task MissingUserDoesNotQueryPermissions()
    {
        var probes = new ConcurrentBag<PermissionUserService>();
        await using ServiceProvider provider = CreateProvider(probes);
        await using AsyncServiceScope requestScope = provider.CreateAsyncScope();
        SetTenant(requestScope, "tenant-a");
        var context = CreateContext(null);

        await requestScope.ServiceProvider.GetRequiredService<IAuthorizationHandler>().HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.Empty(probes);
    }

    [Fact]
    public async Task UninitializedTenantDoesNotFallBackToRootPermissions()
    {
        var probes = new ConcurrentBag<PermissionUserService>();
        await using ServiceProvider provider = CreateProvider(probes);
        await using AsyncServiceScope requestScope = provider.CreateAsyncScope();
        var context = CreateContext("root-user");

        await requestScope.ServiceProvider.GetRequiredService<IAuthorizationHandler>().HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.Empty(probes);
    }

    [Fact]
    public async Task MissingTenantIdDoesNotQueryPermissions()
    {
        var probes = new ConcurrentBag<PermissionUserService>();
        await using ServiceProvider provider = CreateProvider(probes);
        await using AsyncServiceScope requestScope = provider.CreateAsyncScope();
        SetTenant(requestScope, null);
        var context = CreateContext("alice");

        await requestScope.ServiceProvider.GetRequiredService<IAuthorizationHandler>().HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.Empty(probes);
    }

    [Fact]
    public async Task PermissionClaimAvoidsDatabaseLookup()
    {
        var probes = new ConcurrentBag<PermissionUserService>();
        await using ServiceProvider provider = CreateProvider(probes);
        await using AsyncServiceScope requestScope = provider.CreateAsyncScope();
        var context = CreateContext("alice", withPermissionClaim: true);

        await requestScope.ServiceProvider.GetRequiredService<IAuthorizationHandler>().HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.Empty(probes);
    }

    [Fact]
    public async Task ConcurrentChecksUseIndependentScopesForTheCurrentTenant()
    {
        var probes = new ConcurrentBag<PermissionUserService>();
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int started = 0;
        await using ServiceProvider provider = CreateProvider(probes, async () =>
        {
            if (Interlocked.Increment(ref started) == 2)
            {
                bothStarted.SetResult();
            }

            await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        });
        await using AsyncServiceScope requestScope = provider.CreateAsyncScope();
        SetTenant(requestScope, "tenant-a");
        IAuthorizationHandler handler = requestScope.ServiceProvider.GetRequiredService<IAuthorizationHandler>();
        var first = CreateContext("alice");
        var second = CreateContext("alice");

        await Task.WhenAll(handler.HandleAsync(first), handler.HandleAsync(second));

        Assert.True(first.HasSucceeded);
        Assert.True(second.HasSucceeded);
        PermissionUserService[] instances = probes.ToArray();
        Assert.Equal(2, instances.Length);
        Assert.NotSame(instances[0].TenantGetter, instances[1].TenantGetter);
        Assert.All(instances, probe =>
        {
            Assert.Equal("tenant-a", probe.TenantIdAtConstruction);
            Assert.True(probe.IsDisposed);
        });
    }

    private static ServiceProvider CreateProvider(
        ConcurrentBag<PermissionUserService> probes,
        Func<Task>? beforeQuery = null)
    {
        var services = new ServiceCollection();
        services.AddTenantServices();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IUserService>(provider =>
        {
            var service = new PermissionUserService(
                provider.GetRequiredService<ITenantGetterService>(), beforeQuery);
            probes.Add(service);
            return service;
        });
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static void SetTenant(AsyncServiceScope scope, string? tenantId)
    {
        scope.ServiceProvider.GetRequiredService<ITenantSetterService>().SetTenant(new CurrentTenantDetails
        {
            Id = tenantId,
            TenantLink = "https://tenant.example.com"
        });
    }

    private static AuthorizationHandlerContext CreateContext(string? userId, bool withPermissionClaim = false)
    {
        var identity = new ClaimsIdentity("test");
        if (userId is not null)
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        }

        if (withPermissionClaim)
        {
            identity.AddClaim(new Claim(AppClaimTypes.Permission, Permission));
        }

        return new AuthorizationHandlerContext([new PermissionRequirement(Permission)], new ClaimsPrincipal(identity), null);
    }

    private sealed class PermissionUserService(ITenantGetterService tenantGetter, Func<Task>? beforeQuery)
        : IUserService, IDisposable
    {
        public ITenantGetterService TenantGetter { get; } = tenantGetter;
        public string? TenantIdAtConstruction { get; } = tenantGetter.Tenant.Id;
        public bool IsDisposed { get; private set; }

        public async Task<Response<bool>> HasPermissionAsync(
            string userId, string permission, CancellationToken cancellationToken = default)
        {
            if (beforeQuery is not null)
            {
                await beforeQuery();
            }

            bool allowed = permission == Permission && (TenantGetter.Tenant.Id, userId) is
                ("tenant-a", "alice") or ("tenant-b", "bob") or ("root", "root-user");
            return Response<bool>.Success(allowed);
        }

        public Task<Response<List<string>>> GetPermissionsAsync(string userId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public void Dispose() => IsDisposed = true;
    }
}
