using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using AditiKraft.Krafter.Backend.Features.Roles;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Tests.Users;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AditiKraft.Krafter.Tests.Roles;

public sealed class RoleClaimAuthorizationTests
{
    [Theory]
    [InlineData("create", "missing")]
    [InlineData("update", "missing")]
    [InlineData("permissions", "missing")]
    [InlineData("read", "missing")]
    [InlineData("create", "revoked")]
    [InlineData("update", "revoked")]
    [InlineData("permissions", "revoked")]
    [InlineData("read", "revoked")]
    [InlineData("create", "granted")]
    [InlineData("update", "granted")]
    [InlineData("permissions", "granted")]
    [InlineData("read", "granted")]
    public async Task EveryPermissionEndpointRequiresItsLiveClaimAuthority(string operation, string authority)
    {
        await using WebApplication app = await AppAsync(authority);
        string roleId = await RoleIdAsync(app.Services);
        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(app.Urls)) };
        using HttpResponseMessage response = operation switch
        {
            "create" => await client.PostAsJsonAsync("/api/roles", new CreateOrUpdateRoleRequest
            { Name = "Created", Description = "Created", Permissions = ["Permissions.Users.Delete"] }),
            "update" => await client.PutAsJsonAsync($"/api/roles/{roleId}", new CreateOrUpdateRoleRequest
            { Name = "Changed", Description = "Changed", Permissions = ["Permissions.Users.Delete"] }),
            "permissions" => await client.PutAsJsonAsync("/api/roles/permissions", new UpdateRolePermissionsRequest
            { RoleId = roleId, Permissions = ["Permissions.Users.Delete"] }),
            _ => await client.GetAsync($"/api/roles/{roleId}/permissions")
        };
        Assert.Equal(authority == "granted" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        await using var verify = app.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await db.SaveChangesAsync());
        if (authority != "granted")
        {
            Assert.Equal("Operator", (await db.Roles.SingleAsync()).Name);
            Assert.DoesNotContain(await db.RoleClaims.ToListAsync(), claim => claim.ClaimValue == "Permissions.Users.Delete");
        }
        else if (operation != "read")
        {
            Assert.Contains(await db.RoleClaims.ToListAsync(), claim => claim.ClaimValue == "Permissions.Users.Delete");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MetadataOnlyRequestsPreserveClaimsWithoutClaimAuthority(bool explicitNull)
    {
        await using WebApplication app = await AppAsync("missing");
        string roleId = await RoleIdAsync(app.Services);
        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(app.Urls)) };
        var request = new Dictionary<string, object?> { ["name"] = "Metadata", ["description"] = "Changed" };
        if (explicitNull) request["permissions"] = null;
        using HttpResponseMessage updated = await client.PutAsJsonAsync($"/api/roles/{roleId}", request);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        request["name"] = "Created";
        using HttpResponseMessage created = await client.PostAsJsonAsync("/api/roles", request);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        await using var verify = app.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await db.Roles.CountAsync());
        Assert.Equal(3, await db.RoleClaims.CountAsync(claim => claim.RoleId == roleId));
    }

    [Fact]
    public async Task DedicatedClaimManagerNeedsNoRoleMetadataPermissionsAndCanClearClaims()
    {
        await using WebApplication app = await AppAsync("claims-only");
        string roleId = await RoleIdAsync(app.Services);
        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(app.Urls)) };
        using HttpResponseMessage read = await client.GetAsync($"/api/roles/{roleId}/permissions");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using HttpResponseMessage clear = await client.PutAsJsonAsync("/api/roles/permissions",
            new UpdateRolePermissionsRequest { RoleId = roleId, Permissions = [] });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        await using var verify = app.Services.CreateAsyncScope();
        Assert.Empty(await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().RoleClaims.ToListAsync());
    }

    [Fact]
    public async Task SharedSynchronizerRejectsStaleClaimWithoutStagingChanges()
    {
        await using WebApplication app = await AppAsync("revoked");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<RolePermissionService>();
        Response result = await service.SynchronizeAsync((await db.Roles.SingleAsync()).Id,
            ["Permissions.Users.Delete"], CancellationToken.None);
        Assert.Equal(403, result.StatusCode);
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal(0, await db.SaveChangesAsync());
        Assert.DoesNotContain(await db.RoleClaims.ToListAsync(), claim => claim.ClaimValue == "Permissions.Users.Delete");
    }

    private static async Task<string> RoleIdAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Roles.SingleAsync()).Id;
    }

    private static async Task<WebApplication> AppAsync(string authority)
    {
        string actorId = Guid.NewGuid().ToString();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        DomainTestServices.Register(builder.Services, Guid.NewGuid().ToString(), actorId);
        builder.Services.AddSingleton(new Actor(actorId));
        builder.Services.AddAuthentication("domain-tests")
            .AddScheme<AuthenticationSchemeOptions, TestAuthentication>("domain-tests", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        builder.Services.AddScoped<CreateRole.Handler>();
        builder.Services.AddScoped<UpdateRole.Handler>();
        builder.Services.AddScoped<UpdateRolePermissions.Handler>();
        builder.Services.AddScoped<GetRoleByIdWithPermissions.Handler>();
        builder.Services.AddValidatorsFromAssemblyContaining<CreateOrUpdateRoleRequestValidator>();
        builder.AddFluentValidationEndpointFilter();
        WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseAuthentication();
        app.UseAuthorization();
        var routes = app.MapGroup("/api");
        new CreateRole.Route().MapRoute(routes);
        new UpdateRole.Route().MapRoute(routes);
        new UpdateRolePermissions.Route().MapRoute(routes);
        new GetRoleByIdWithPermissions.Route().MapRoute(routes);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var actor = await DomainTestServices.AddUserAsync(scope.ServiceProvider, "actor@example.com", actorId);
            var role = await DomainTestServices.AddRoleAsync(scope.ServiceProvider, "Operator");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.UserRoles.Add(new ApplicationUserRole { UserId = actor.Id, RoleId = role.Id });
            string[] metadata = authority == "claims-only" ? [] :
                ["Permissions.Roles.Create", "Permissions.Roles.Update", "Permissions.Roles.View"];
            foreach (string permission in metadata)
                db.RoleClaims.Add(new ApplicationRoleClaim { RoleId = role.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = permission });
            if (authority != "missing")
                foreach (string permission in new[] { "Permissions.RoleClaims.View", "Permissions.RoleClaims.Update" })
                    db.RoleClaims.Add(new ApplicationRoleClaim
                    { RoleId = role.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = permission, IsDeleted = authority == "revoked" });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        return app;
    }

    public sealed record Actor(string Id);
    public sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, Actor actor)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(DomainTestServices.Principal(actor.Id), "domain-tests")));
    }
}
