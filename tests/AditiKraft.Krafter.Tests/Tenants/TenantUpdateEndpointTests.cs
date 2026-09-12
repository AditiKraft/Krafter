using System.Net;
using System.Net.Http.Json;
using AditiKraft.Krafter.Backend.Features.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Tenants;

public sealed class TenantUpdateEndpointTests
{
    [Theory]
    [InlineData("root", null, HttpStatusCode.OK)]
    [InlineData("root", "wrong-body-id", HttpStatusCode.OK)]
    [InlineData("blue-id", "root", HttpStatusCode.BadRequest)]
    public async Task UpdateValidationUsesRouteIdInsteadOfBodyId(string routeId, string? bodyId,
        HttpStatusCode expectedStatus)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        string databaseName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<TenantDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddSingleton(new AppUrls
        {
            RootUiUrl = "https://krafter.getkrafter.dev",
            TenantBaseDomain = "getkrafter.dev",
            ApiBaseUrl = "https://api.getkrafter.dev"
        });
        builder.Services.AddScoped<UpdateTenant.Handler>();
        builder.Services.AddValidatorsFromAssemblyContaining<CreateOrUpdateTenantRequestValidator>();
        builder.AddFluentValidationEndpointFilter();
        builder.Services.AddAuthorization(options => options.AddPolicy(
            PermissionDefinition.NameFor(PermissionAction.Update, PermissionResource.Tenants),
            policy => policy.RequireAssertion(_ => true)));
        await using WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        // Keep the production route and filters. Authentication is outside this validation regression.
        new UpdateTenant.Route().MapRoute(app.MapGroup("").AllowAnonymous());

        DateTime validUntil = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            TenantDbContext db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
            db.Tenants.AddRange(
                new Tenant
                {
                    Id = "root", Identifier = "root", Name = "Root tenant", AdminEmail = "admin@example.com",
                    IsActive = true, ValidUpto = validUntil
                },
                new Tenant
                {
                    Id = "blue-id", Identifier = "blue", Name = "Blue tenant", AdminEmail = "admin@example.com",
                    IsActive = true, ValidUpto = validUntil
                });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();

        var request = new Dictionary<string, object?>
        {
            ["identifier"] = "root", ["name"] = "Updated root", ["adminEmail"] = "admin@example.com",
            ["isActive"] = true, ["validUpto"] = validUntil
        };
        if (bodyId is not null)
        {
            request["id"] = bodyId;
        }
        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(app.Urls)) };
        using HttpResponseMessage response = await client.PutAsJsonAsync($"/{ApiRoutes.Tenants}/{routeId}", request);

        Assert.Equal(expectedStatus, response.StatusCode);
        await using AsyncServiceScope verificationScope = app.Services.CreateAsyncScope();
        TenantDbContext savedDb = verificationScope.ServiceProvider.GetRequiredService<TenantDbContext>();
        Tenant root = await savedDb.Tenants.SingleAsync(tenant => tenant.Id == "root");
        Assert.Equal(routeId == "root" ? "Updated root" : "Root tenant", root.Name);
        Tenant blue = await savedDb.Tenants.SingleAsync(tenant => tenant.Id == "blue-id");
        Assert.Equal("blue", blue.Identifier);
        Assert.Equal("Blue tenant", blue.Name);
    }
}
