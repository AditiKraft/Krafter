using System.Net;
using System.Text;
using System.Text.Json;
using AditiKraft.Krafter.Backend.Features.Roles;
using AditiKraft.Krafter.Backend.Features.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using AditiKraft.Krafter.Tests.Auth;
using AditiKraft.Krafter.Tests.Users;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Validation;

[Collection("MFA PostgreSQL")]
public sealed class PersistedTextEndpointTests
{
    [Fact]
    public Task MutationRoutesRejectInvalidTextBeforeWritingAndKeepUnicode()
        => VerifyAsync(null);

    [PostgresMfaFact]
    public Task PostgreSqlMutationRoutesRejectInvalidTextBeforeWritingAndPersistUnicode()
        => VerifyAsync(Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!);

    private static async Task VerifyAsync(string? postgres)
    {
        string tenantId = Guid.NewGuid().ToString();
        string existingEmail = $"existing@{tenantId}.test";
        string identifier = Guid.NewGuid().ToString("N")[..10];
        var saves = new SaveAttemptCounter();
        await using WebApplication app = await CreateAppAsync(tenantId, postgres, saves);
        if (postgres is not null) await DomainTestServices.MigrateAsync(app.Services);
        string userId, roleId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await DomainTestServices.AddRoleAsync(services, RoleConstants.Basic);
            var role = await DomainTestServices.AddRoleAsync(services, "Existing");
            role.Description = "Original role";
            roleId = role.Id;
            var user = await DomainTestServices.AddUserAsync(services, existingEmail);
            userId = user.Id;
            await services.GetRequiredService<ApplicationDbContext>().SaveChangesAsync();
            var registry = services.GetRequiredService<TenantDbContext>();
            registry.Tenants.Add(new Tenant
            {
                Id = tenantId, Identifier = identifier, Name = "Original tenant", AdminEmail = existingEmail,
                IsActive = true, ValidUpto = Expiry
            });
            await registry.SaveChangesAsync();
        }

        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(app.Urls)) };
        var mutations = new (HttpMethod Method, string Path, object Payload, string[] Fields)[]
        {
            // Description reaches the provider on the unfixed implementation, without Identity rejecting it first.
            (HttpMethod.Post, "/roles/", Role("Created"), ["Description", "Name"]),
            (HttpMethod.Put, $"/roles/{roleId}", Role("Existing"), ["Description", "Name"]),
            (HttpMethod.Post, "/users/", User($"new@{tenantId}.test"), ["FirstName", "LastName", "PhoneNumber", "Email", "UserName", "Roles"]),
            (HttpMethod.Put, $"/users/{userId}", User(existingEmail), ["FirstName", "LastName", "PhoneNumber", "Email", "UserName", "Roles"]),
            (HttpMethod.Post, "/tenants/", TenantRequest(Guid.NewGuid().ToString("N")[..10], existingEmail),
                ["Name", "AdminEmail", "TablesToCopy"]),
            (HttpMethod.Put, $"/tenants/{tenantId}", TenantRequest(identifier, existingEmail),
                ["Name", "AdminEmail", "TablesToCopy"])
        };
        var jobs = app.Services.GetRequiredService<DomainJobs>();
        foreach (var mutation in mutations)
        foreach (string field in mutation.Fields)
        foreach (string escape in new[] { "\\u0000", "\\uD800", "\\uDC00" })
        {
            var json = JsonSerializer.SerializeToNode(mutation.Payload)!.AsObject();
            json[field] = "INVALID_TEXT_SENTINEL";
            string value = field is "Email" or "AdminEmail" ? $"a{escape}b@example.test" : $"a{escape}b";
            string replacement = field == "Roles" ? $"[\"{value}\"]" : $"\"{value}\"";
            string body = json.ToJsonString().Replace("\"INVALID_TEXT_SENTINEL\"", replacement, StringComparison.Ordinal);
            saves.Reset();
            using var request = new HttpRequestMessage(mutation.Method, mutation.Path)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            using HttpResponseMessage response = await client.SendAsync(request);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
                $"{mutation.Method} {mutation.Path} {field} {escape}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            Assert.Equal(0, saves.Count);
            Assert.Empty(jobs.Emails);
        }

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(user => user.Id == userId);
            Assert.Equal("First", user.FirstName);
            Assert.Equal("Last", user.LastName);
            Assert.Null(user.PhoneNumber);
            Assert.Equal("Original role", (await db.Roles.SingleAsync(role => role.Id == roleId)).Description);
            Assert.Equal("Original tenant", (await scope.ServiceProvider.GetRequiredService<TenantDbContext>()
                .Tenants.SingleAsync(tenant => tenant.Id == tenantId)).Name);
        }

        // Positive controls use the same routes and actual provider, including tenant provisioning.
        var createdUser = User($"unicode@{tenantId}.test");
        createdUser.FirstName = "First😀";
        createdUser.LastName = "Last😀";
        createdUser.PhoneNumber = "123😀";
        await SuccessAsync(client, HttpMethod.Post, "/users/", createdUser);
        var updatedUser = User(existingEmail);
        updatedUser.FirstName = "Updated😀";
        updatedUser.LastName = "Surname😀";
        updatedUser.PhoneNumber = "456😀";
        await SuccessAsync(client, HttpMethod.Put, $"/users/{userId}", updatedUser);
        await SuccessAsync(client, HttpMethod.Post, "/roles/", Role("Created😀"));
        await SuccessAsync(client, HttpMethod.Put, $"/roles/{roleId}", Role("Updated😀"));
        string createdIdentifier = Guid.NewGuid().ToString("N")[..10];
        var createdTenant = TenantRequest(createdIdentifier, $"owner@{tenantId}.test");
        createdTenant.TablesToCopy = "schema😀";
        await SuccessAsync(client, HttpMethod.Post, "/tenants/", createdTenant);
        await SuccessAsync(client, HttpMethod.Put, $"/tenants/{tenantId}", TenantRequest(identifier, existingEmail));

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var created = await db.Users.SingleAsync(user => user.Email == createdUser.Email);
            Assert.Equal((createdUser.FirstName, createdUser.LastName, createdUser.PhoneNumber),
                (created.FirstName, created.LastName, created.PhoneNumber));
            var updated = await db.Users.SingleAsync(user => user.Id == userId);
            Assert.Equal((updatedUser.FirstName, updatedUser.LastName, updatedUser.PhoneNumber),
                (updated.FirstName, updated.LastName, updated.PhoneNumber));
            Assert.Equal("Description😀", (await db.Roles.SingleAsync(role => role.Name == "Created😀")).Description);
            var updatedRole = await db.Roles.SingleAsync(role => role.Id == roleId);
            Assert.Equal(("Updated😀", "Description😀"), (updatedRole.Name, updatedRole.Description));
            var registry = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
            var tenant = await registry.Tenants.SingleAsync(tenant => tenant.Identifier == createdIdentifier);
            Assert.Equal(("Tenant😀", "schema😀"), (tenant.Name, tenant.TablesToCopy));
            Assert.Equal("Tenant😀", (await registry.Tenants.SingleAsync(tenant => tenant.Id == tenantId)).Name);
        }

        // Display text may contain emoji; Identity's default username alphabet remains authoritative.
        var invalidUsername = User($"identity@{tenantId}.test");
        invalidUsername.UserName = "user😀";
        saves.Reset();
        using var identityRequest = new HttpRequestMessage(HttpMethod.Post, "/users/")
        { Content = new StringContent(JsonSerializer.Serialize(invalidUsername), Encoding.UTF8, "application/json") };
        using HttpResponseMessage identityResponse = await client.SendAsync(identityRequest);
        Assert.Equal(HttpStatusCode.BadRequest, identityResponse.StatusCode);
        Assert.Equal(0, saves.Count);
    }

    private static async Task SuccessAsync(HttpClient client, HttpMethod method, string path, object payload)
    {
        using var request = new HttpRequestMessage(method, path)
        { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
        using HttpResponseMessage response = await client.SendAsync(request);
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"{method} {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    private static readonly DateTime Expiry = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static CreateUserRequest User(string email) => new()
    { FirstName = "First", LastName = "Last", Email = email, UserName = email, PhoneNumber = "123", UpdateTenantEmail = false };
    private static CreateOrUpdateRoleRequest Role(string name) => new() { Name = name, Description = "Description😀" };
    private static CreateOrUpdateTenantRequest TenantRequest(string identifier, string email) => new()
    { Name = "Tenant😀", Identifier = identifier, AdminEmail = email, IsActive = true, ValidUpto = Expiry };

    private static async Task<WebApplication> CreateAppAsync(string tenantId, string? postgres, SaveAttemptCounter saves)
    {
        var builder = WebApplication.CreateBuilder();
        DomainTestServices.Register(builder.Services, tenantId, postgres: postgres,
            identityInterceptor: saves, registryInterceptor: saves);
        builder.Services.AddSingleton(DomainTestServices.Urls);
        builder.Services.AddScoped<CreateUser.Handler>();
        builder.Services.AddScoped<UpdateUser.Handler>();
        builder.Services.AddScoped<CreateRole.Handler>();
        builder.Services.AddScoped<UpdateRole.Handler>();
        builder.Services.AddScoped<CreateTenant.Handler>();
        builder.Services.AddScoped<UpdateTenant.Handler>();
        builder.Services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>();
        builder.AddFluentValidationEndpointFilter();
        builder.Services.AddAuthorization(options =>
        {
            foreach (string resource in new[] { PermissionResource.Users, PermissionResource.Roles, PermissionResource.Tenants })
            foreach (string action in new[] { PermissionAction.Create, PermissionAction.Update })
                options.AddPolicy(PermissionDefinition.NameFor(action, resource), policy => policy.RequireAssertion(_ => true));
        });
        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        // Authentication/authorization have separate regressions; these are the production binding and validation filters.
        var routes = app.MapGroup("").AllowAnonymous();
        new CreateUser.Route().MapRoute(routes);
        new UpdateUser.Route().MapRoute(routes);
        new CreateRole.Route().MapRoute(routes);
        new UpdateRole.Route().MapRoute(routes);
        new CreateTenant.Route().MapRoute(routes);
        new UpdateTenant.Route().MapRoute(routes);
        await app.StartAsync();
        return app;
    }

    private sealed class SaveAttemptCounter : SaveChangesInterceptor
    {
        private int count;
        public int Count => Volatile.Read(ref count);
        public void Reset() => Interlocked.Exchange(ref count, 0);
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Interlocked.Increment(ref count);
            return result;
        }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref count);
            return ValueTask.FromResult(result);
        }
    }
}
