using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Tests.Users;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AditiKraft.Krafter.Tests.Authorization;

public sealed class RoleMemberAuthorizationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RoleMemberRouteRequiresUserAndMembershipViewAuthority(bool canViewUsers, bool canViewMemberships)
    {
        string actorId = Guid.NewGuid().ToString();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        DomainTestServices.Register(builder.Services, Guid.NewGuid().ToString(), actorId);
        builder.Services.AddSingleton(new Actor(actorId));
        builder.Services.AddAuthentication("membership-tests")
            .AddScheme<AuthenticationSchemeOptions, TestAuthentication>("membership-tests", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        builder.Services.AddScoped<GetUsersByRole.Handler>();
        builder.Services.AddScoped<GetUserRoles.Handler>();
        builder.Services.AddScoped<GetUsers.Handler>();
        builder.AddFluentValidationEndpointFilter();
        await using WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseAuthentication();
        app.UseAuthorization();
        var routes = app.MapGroup("/api");
        new GetUsersByRole.Route().MapRoute(routes);
        new GetUserRoles.Route().MapRoute(routes);
        new GetUsers.Route().MapRoute(routes);
        string memberId;
        string memberRoleId;
        await using (var seed = app.Services.CreateAsyncScope())
        {
            var services = seed.ServiceProvider;
            await DomainTestServices.AddUserAsync(services, "viewer@example.com", actorId);
            var viewerRole = await DomainTestServices.AddRoleAsync(services, "Viewer");
            memberId = (await DomainTestServices.AddUserAsync(services, "member@example.com")).Id;
            memberRoleId = (await DomainTestServices.AddRoleAsync(services, "Members")).Id;
            var db = services.GetRequiredService<ApplicationDbContext>();
            db.UserRoles.AddRange(new ApplicationUserRole { UserId = actorId, RoleId = viewerRole.Id },
                new ApplicationUserRole { UserId = memberId, RoleId = memberRoleId });
            if (canViewUsers)
                db.RoleClaims.Add(new ApplicationRoleClaim
                { RoleId = viewerRole.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = "Permissions.Users.View" });
            if (canViewMemberships)
                db.RoleClaims.Add(new ApplicationRoleClaim
                { RoleId = viewerRole.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = "Permissions.UserRoles.View" });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(app.Urls)) };
        using HttpResponseMessage users = await client.GetAsync("/api/users");
        Assert.Equal(canViewUsers ? HttpStatusCode.OK : HttpStatusCode.Forbidden, users.StatusCode);
        using HttpResponseMessage memberships = await client.GetAsync($"/api/users/{memberId}/roles");
        Assert.Equal(canViewMemberships ? HttpStatusCode.OK : HttpStatusCode.Forbidden, memberships.StatusCode);

        using HttpResponseMessage members = await client.GetAsync($"/api/users/by-role/{memberRoleId}");

        Assert.Equal(canViewUsers && canViewMemberships ? HttpStatusCode.OK : HttpStatusCode.Forbidden, members.StatusCode);
        if (members.IsSuccessStatusCode)
        {
            var response = await members.Content.ReadFromJsonAsync<Response<PaginationResponse<UserInfo>>>();
            UserInfo member = Assert.Single(response!.Data!.Items);
            Assert.Equal(memberId, member.Id);
            Assert.Equal("member@example.com", member.Email);
        }
    }

    public sealed record Actor(string Id);

    public sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, Actor actor)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(DomainTestServices.Principal(actor.Id), "membership-tests")));
    }
}
