using System.Security.Claims;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Roles;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.Tests.Auth;
using AditiKraft.Krafter.Tests.Roles;
using AditiKraft.Krafter.UI.Web.Client.Common.Constants;
using AditiKraft.Krafter.UI.Web.Client.Common.Permissions;
using AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;
using AditiKraft.Krafter.UI.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace AditiKraft.Krafter.Tests.Authorization;

public sealed class PermissionRevocationTests
{
    private const string Permission = "Permissions.Users.Delete";

    [Theory]
    [InlineData("permission", false)]
    [InlineData("permission", true)]
    [InlineData("membership", false)]
    [InlineData("membership", true)]
    [InlineData("role", false)]
    [InlineData("role", true)]
    public async Task RevocationAppliesToBearerAndCookiePrincipalsWithStaleCachedPermissions(string change, bool cookie)
    {
        using var harness = new MfaHarness();
        using MfaScope seed = harness.Scope();
        var user = await seed.AddUserAsync();
        var role = new ApplicationRole("Operator") { Id = Guid.NewGuid().ToString() };
        Assert.True((await seed.Services.GetRequiredService<RoleManager<ApplicationRole>>().CreateAsync(role)).Succeeded);
        seed.Db.UserRoles.Add(new ApplicationUserRole { UserId = user.Id, RoleId = role.Id });
        seed.Db.RoleClaims.Add(new ApplicationRoleClaim
        { RoleId = role.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = Permission });
        await seed.Db.SaveChangesAsync();
        ApplicationUser manager = await RoleAuthority.CreateManagerAsync(seed);
        TokenResponse token = (await seed.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test")).Data!.Tokens!;

        var accessor = new HttpContextAccessor();
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddHybridCache();
        registrations.AddSingleton<IHttpContextAccessor>(accessor);
        registrations.AddScoped<IAuthStorageService, AuthStorageServiceServer>();
        registrations.AddTenantServices();
        // Match single-host composition: backend dynamic policies plus the UI's registered display policies.
        registrations.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        registrations.AddScoped<IAuthorizationHandler>(services => new PermissionAuthorizationHandler(
            harness.Services.GetRequiredService<IServiceScopeFactory>(), services.GetRequiredService<ITenantGetterService>()));
        registrations.AddAuthorizationCore(PermissionRegistration.RegisterPermissionClaims);
        using ServiceProvider ui = registrations.BuildServiceProvider();
        // Keep the original server cache intact across the admin's change, just as a live cookie request does.
        await ui.GetRequiredService<HybridCache>().SetAsync(
            $"{StorageConstants.Local.Permissions}_{user.Id}", new List<string> { Permission });
        Assert.True(await AuthorizeAsync());

        using (MfaScope admin = harness.Scope())
        {
            if (change == "role")
            {
                Assert.False((await new DeleteRole.Handler(
                    admin.Services.GetRequiredService<RoleManager<ApplicationRole>>(), admin.Db).DeleteAsync(role.Id)).IsError);
            }
            else if (change == "membership")
            {
                (await admin.Db.UserRoles.SingleAsync(link => link.UserId == user.Id)).IsDeleted = true;
                await admin.Db.SaveChangesAsync();
            }
            else
            {
                var permissions = RoleAuthority.Service(admin, manager);
                Assert.False((await permissions.SynchronizeAsync(role.Id, [], CancellationToken.None)).IsError);
                await admin.Db.SaveChangesAsync();
            }
        }

        Assert.False(await AuthorizeAsync());

        async Task<bool> AuthorizeAsync()
        {
            using MfaScope request = harness.Scope();
            using IServiceScope uiRequest = ui.CreateScope();
            var http = new DefaultHttpContext { RequestServices = uiRequest.ServiceProvider };
            http.Request.Path = "/api/users/example";
            if (cookie)
                http.Request.Headers.Cookie = $"{StorageConstants.Local.AuthToken}={token.Token}";
            else
                http.Request.Headers.Authorization = $"Bearer {token.Token}";
            accessor.HttpContext = http;
            // JWT cryptographic validation is covered elsewhere; exercise the actual single-host
            // session validation/enrichment and backend authorization stages with the issued principal.
            var validated = new TokenValidatedContext(http,
                new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)), new JwtBearerOptions())
            { Principal = MfaHarness.Principal(token), SecurityToken = new JsonWebToken(token.Token) };
            await new BlazorJwtBearerEvents(BlazorHostingMode.SingleHost,
                context => request.Sessions.ValidateAsync(context.Principal!)).TokenValidated(validated);
            Assert.Null(validated.Result?.Failure);
            Assert.Equal(cookie, validated.Principal!.HasClaim(AppClaimTypes.Permission, Permission));
            IAuthorizationService authorization = uiRequest.ServiceProvider.GetRequiredService<IAuthorizationService>();
            string displayPolicy = UiPermissionPolicy.NameFor(PermissionAction.Delete, PermissionResource.Users);
            // Component display checks work even in a server circuit whose backend tenant scope is uninitialized.
            Assert.Equal(cookie, (await authorization.AuthorizeAsync(validated.Principal, null, displayPolicy)).Succeeded);
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AppClaimTypes.Permission, Permission)]));
            Assert.False((await authorization.AuthorizeAsync(anonymous, null, displayPolicy)).Succeeded);

            uiRequest.ServiceProvider.GetRequiredService<ITenantSetterService>()
                .SetTenant(request.Services.GetRequiredService<ITenantGetterService>().Tenant);
            return (await authorization.AuthorizeAsync(validated.Principal, http, Permission)).Succeeded;
        }
    }
}
