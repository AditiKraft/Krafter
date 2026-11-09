using System.Security.Claims;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Infrastructure.Realtime;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class RealtimePermissionRevocationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RemovingPermissionOrMembershipClosesExistingSocketDespiteCachedClaimsAsync(bool periodic, bool removeMembership)
    {
        using var harness = new MfaHarness();
        using MfaScope seed = harness.Scope();
        TenantDbContext tenants = seed.Services.GetRequiredService<TenantDbContext>();
        tenants.Tenants.Add(new Tenant
        {
            Id = "test", Identifier = "test", Name = "Test", AdminEmail = "owner@example.test",
            IsActive = true, ValidUpto = DateTime.MaxValue
        });
        await tenants.SaveChangesAsync();
        var user = await seed.AddUserAsync();
        ApplicationRole role = await RealtimePermissionTestData.GrantAsync(seed, user.Id);
        var login = await seed.Mfa.PasswordLoginAsync(MfaHarness.PasswordRequest(), "test");
        ClaimsPrincipal principal = MfaHarness.Principal(login.Data!.Tokens!);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(AppClaimTypes.Permission, RealtimePermissionTestData.Permission));
        CurrentTenantDetails tenant = seed.Services.GetRequiredService<ITenantGetterService>().Tenant;

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddScoped<CurrentTenantService>();
        builder.Services.AddScoped<ITenantGetterService>(services => services.GetRequiredService<CurrentTenantService>());
        builder.Services.AddScoped<ITenantSetterService>(services => services.GetRequiredService<CurrentTenantService>());
        builder.Services.AddSingleton<SessionHubFilter>(services => new SessionHubFilter(
            harness.Services.GetRequiredService<IServiceScopeFactory>(), services.GetRequiredService<IHostApplicationLifetime>(),
            services.GetRequiredService<ILogger<SessionHubFilter>>()));
        builder.Services.AddRealtimeServices();
        await using WebApplication app = builder.Build();
        // Supply a verified principal and resolved tenant; use the actual app hub/filter registration.
        app.Use((context, next) =>
        {
            context.User = principal;
            context.Items[CurrentTenantService.ResolvedTenantItemKey] = tenant;
            context.RequestServices.GetRequiredService<ITenantSetterService>().SetTenant(tenant);
            return next(context);
        });
        app.MapHub<RealtimeHub>("/api/RealtimeHub");
        await app.StartAsync();
        try
        {
            await using HubConnection connection = new HubConnectionBuilder()
                .WithUrl(app.Urls.Single() + "/api/RealtimeHub", options => options.Transports = HttpTransportType.WebSockets)
                .Build();
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.Closed += _ => { closed.TrySetResult(); return Task.CompletedTask; };
            await connection.StartAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await connection.InvokeAsync(nameof(RealtimeHub.SendMessageAsync), "user", "allowed")
                .WaitAsync(TimeSpan.FromSeconds(10));

            using MfaScope changed = harness.Scope();
            if (removeMembership)
            {
                var membership = await changed.Db.UserRoles.SingleAsync(link => link.UserId == user.Id && link.RoleId == role.Id);
                membership.IsDeleted = true;
            }
            else
            {
                var permission = await changed.Db.RoleClaims.SingleAsync(claim => claim.RoleId == role.Id);
                permission.IsDeleted = true;
            }
            await changed.Db.SaveChangesAsync();
            Assert.True(await changed.Sessions.ValidateAsync(principal));
            Assert.True(principal.HasClaim(AppClaimTypes.Permission, RealtimePermissionTestData.Permission));

            if (!periodic)
            {
                Assert.NotNull(await Record.ExceptionAsync(async () =>
                    await connection.InvokeAsync(nameof(RealtimeHub.SendMessageAsync), "user", "revoked")
                        .WaitAsync(TimeSpan.FromSeconds(10))));
            }
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(12));
        }
        finally
        {
            await app.StopAsync();
        }
    }
}

internal static class RealtimePermissionTestData
{
    public static string Permission => PermissionDefinition.NameFor(PermissionAction.View, PermissionResource.Notifications);

    public static async Task<ApplicationRole> GrantAsync(MfaScope scope, string userId)
    {
        var role = new ApplicationRole("RealtimeReader") { Id = Guid.NewGuid().ToString() };
        RoleManager<ApplicationRole> roles = scope.Services.GetRequiredService<RoleManager<ApplicationRole>>();
        Assert.True((await roles.CreateAsync(role)).Succeeded);
        scope.Db.UserRoles.Add(new ApplicationUserRole { UserId = userId, RoleId = role.Id });
        scope.Db.RoleClaims.Add(new ApplicationRoleClaim
        {
            RoleId = role.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = Permission
        });
        await scope.Db.SaveChangesAsync();
        return role;
    }
}
