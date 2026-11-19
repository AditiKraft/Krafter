using System.Data.Common;
using System.Net;
using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Roles;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using AditiKraft.Krafter.Tests.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Radzen;

namespace AditiKraft.Krafter.Tests.Queries;

[Collection("MFA PostgreSQL")]
public sealed class ListQueryPostgresTests
{
    [PostgresMfaFact]
    public async Task ValidGridFiltersExecuteAndInvalidQueriesNeverReachPostgres()
    {
        string connection = Environment.GetEnvironmentVariable("KRAFTER_TEST_POSTGRES")!;
        var commands = new CountReaders();
        var tenant = new CurrentTenantService();
        string tenantId = Guid.NewGuid().ToString();
        tenant.SetTenant(new CurrentTenantDetails { Id = tenantId, TenantLink = "https://list.example.test" });
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection).AddInterceptors(commands).Options, new CurrentUser(), tenant);
        await using var tenantDb = new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(connection).AddInterceptors(commands).Options);
        await db.Database.MigrateAsync();
        await tenantDb.Database.MigrateAsync();
        // Dispose both transactions without committing so test rows cannot leak into later runs.
        await using var identityTransaction = await db.Database.BeginTransactionAsync();
        await using var registryTransaction = await tenantDb.Database.BeginTransactionAsync();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(), UserName = "list@example.test", Email = "list@example.test",
            FirstName = "Alice😀", IsActive = true
        };
        var missingName = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(), UserName = "missing@example.test", Email = "missing@example.test",
            FirstName = null, IsActive = true
        };
        var role = new ApplicationRole("Alice Members") { Id = Guid.NewGuid().ToString() };
        db.Users.Add(user);
        db.Users.Add(missingName);
        db.Roles.Add(role);
        db.UserRoles.Add(new ApplicationUserRole { UserId = user.Id, RoleId = role.Id });
        db.UserRoles.Add(new ApplicationUserRole { UserId = missingName.Id, RoleId = role.Id });
        await db.SaveChangesAsync();
        tenantDb.Tenants.Add(new Tenant
        {
            Id = tenantId, Identifier = "l" + Guid.NewGuid().ToString("N")[..9], Name = "Alice Tenant",
            AdminEmail = "list@example.test", IsActive = true, CreatedOn = DateTime.UtcNow,
            ValidUpto = DateTime.UtcNow.AddDays(1)
        });
        await tenantDb.SaveChangesAsync();
        db.ChangeTracker.Clear();
        tenantDb.ChangeTracker.Clear();

        var users = await new GetUsers.Handler(db).GetAsync(
            new GetRequestInput { Filter = GridFilter<UserDto>(nameof(UserDto.FirstName)), OrderBy = "FirstName asc, CreatedOn desc" },
            CancellationToken.None);
        Assert.Equal(user.Id, Assert.Single(users.Data!.Items).Id);
        var roles = await new GetRoles.Handler(db).GetListAsync(
            new GetRequestInput { Filter = GridFilter<RoleDto>(nameof(RoleDto.Name)) }, CancellationToken.None);
        Assert.Equal(role.Id, Assert.Single(roles.Data!.Items).Id);
        var members = await new GetUsersByRole.Handler(db).GetByRoleAsync(role.Id,
            new GetRequestInput { Filter = GridFilter<UserInfo>(nameof(UserInfo.FirstName)) }, CancellationToken.None);
        Assert.Equal(user.Email, Assert.Single(members.Data!.Items).Email);
        var tenants = await new GetTenants.Handler(tenantDb).GetAsync(
            new GetRequestInput { Id = tenantId, Filter = GridFilter<TenantDto>(nameof(TenantDto.Name)) }, CancellationToken.None);
        Assert.Equal(tenantId, Assert.Single(tenants.Data!.Items).Id);
        var unicode = await new GetUsers.Handler(db).GetAsync(
            new GetRequestInput { Filter = "FirstName == \"Alice\\uD83D\\uDE00\"", Query = "😀" }, CancellationToken.None);
        Assert.Equal(user.Id, Assert.Single(unicode.Data!.Items).Id);
        const string userNullPropagation = "np(FirstName, \"\").ToLower().Contains(\"alice\")";
        const string nameNullPropagation = "np(Name, \"\").ToLower().Contains(\"alice\")";
        var propagatedUsers = await new GetUsers.Handler(db).GetAsync(
            new GetRequestInput { Filter = userNullPropagation }, CancellationToken.None);
        Assert.Equal(user.Id, Assert.Single(propagatedUsers.Data!.Items).Id);
        var propagatedMembers = await new GetUsersByRole.Handler(db).GetByRoleAsync(role.Id,
            new GetRequestInput { Filter = userNullPropagation }, CancellationToken.None);
        Assert.Equal(user.Email, Assert.Single(propagatedMembers.Data!.Items).Email);
        var propagatedRoles = await new GetRoles.Handler(db).GetListAsync(
            new GetRequestInput { Filter = nameNullPropagation }, CancellationToken.None);
        Assert.Equal(role.Id, Assert.Single(propagatedRoles.Data!.Items).Id);
        var propagatedTenants = await new GetTenants.Handler(tenantDb).GetAsync(
            new GetRequestInput { Id = tenantId, Filter = nameNullPropagation }, CancellationToken.None);
        Assert.Equal(tenantId, Assert.Single(propagatedTenants.Data!.Items).Id);
        foreach (string filter in new[]
        {
            "np(FirstName).ToLower().Contains(\"alice\")",
            "(FirstName != null ? FirstName : \"\").ToLower().Contains(\"alice\")",
            "(it != null && FirstName != null ? FirstName : \"\").ToLower().Contains(\"alice\")",
            "(it == null ? \"\" : FirstName).ToLower().Contains(\"alice\")",
            "(it != null ? FirstName : null).ToLower().Contains(\"alice\")"
        })
        {
            var guardedUsers = await new GetUsers.Handler(db).GetAsync(
                new GetRequestInput { Filter = filter }, CancellationToken.None);
            Assert.Equal(user.Id, Assert.Single(guardedUsers.Data!.Items).Id);
        }
        var fallback = await new GetUsers.Handler(db).GetAsync(new GetRequestInput
        {
            Filter = "np(FirstName, \"missing\").ToLower().Contains(\"missing\")"
        }, CancellationToken.None);
        Assert.Equal(missingName.Id, Assert.Single(fallback.Data!.Items).Id);

        string[] unsupportedRootFilters =
        [
            "it == null", "it != null", "it == it", "it != it", "(Id != null ? it : null) != null",
            "(it == null ? Id : null).ToLower() == \"x\"",
            "(it != null ? null : Id).Contains(\"x\")",
            "(it == null && Id != null ? Id : null).ToLower() == \"x\"",
            "(it != null || Id != null ? null : Id).ToLower() == \"x\"",
            "(it != null ? Id : \"a\\u0000b\").ToLower() == \"x\"",
            "(it == null ? Id.PadLeft(100) : Id).ToLower() == \"x\""
        ];
        commands.Readers = 0;
        GetRequestInput[] invalid =
        [
            new() { SkipCount = -1 }, new() { MaxResultCount = -1 }, new() { MaxResultCount = 0 },
            new() { MaxResultCount = int.MaxValue }, new() { OrderBy = "NoSuchProperty" },
            new() { Filter = "Id ==" }, new() { Filter = "Id.PadLeft(100) == \"x\"" },
            .. unsupportedRootFilters.Select(filter => new GetRequestInput { Filter = filter }),
            new() { Query = "a\0b" }, new() { Id = "a\0b" }, new() { Filter = "Id == \"a\0b\"" }, new() { OrderBy = "Id\0" },
            new() { Filter = "Id == \"a\\u0000b\"" }, new() { Filter = "Id == \"a\\uD800b\"" },
            new() { Filter = "Id == \"a\\uDC00b\"" }, new() { Query = "a\ud800b" },
            new() { Filter = "(true ? null : \"x\").ToLower() == \"x\"" },
            new() { Filter = ListQueryEndpointTests.DeepConditionalFilter() }
        ];
        foreach (GetRequestInput request in invalid)
        {
            Assert.Equal(400, (await new GetUsers.Handler(db).GetAsync(request, CancellationToken.None)).StatusCode);
            Assert.Equal(400, (await new GetRoles.Handler(db).GetListAsync(request, CancellationToken.None)).StatusCode);
            Assert.Equal(400, (await new GetUsersByRole.Handler(db).GetByRoleAsync(role.Id, request, CancellationToken.None)).StatusCode);
            Assert.Equal(400, (await new GetTenants.Handler(tenantDb).GetAsync(request, CancellationToken.None)).StatusCode);
        }
        Assert.Equal(400, (await new GetUsersByRole.Handler(db).GetByRoleAsync("a\0b", new(), CancellationToken.None)).StatusCode);
        Assert.Equal(0, commands.Readers);

        await using var app = await ListQueryEndpointTests.CreateAppAsync(db, tenantDb);
        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(app.Urls)) };
        string[] paths = ["/users/", "/roles/", "/tenants/", $"/users/by-role/{role.Id}"];
        foreach (string filter in unsupportedRootFilters)
        {
            foreach (string path in paths)
            {
                using HttpResponseMessage response = await client.GetAsync(path + "?Filter=" + Uri.EscapeDataString(filter));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
        }
        Assert.Equal(0, commands.Readers);

        foreach (string path in paths)
        {
            using HttpResponseMessage response = await client.GetAsync(path + "?Filter=" + Uri.EscapeDataString("Id != null"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        Assert.True(commands.Readers > 0);
    }

    private static string GridFilter<T>(string property) => new[]
    {
        new CompositeFilterDescriptor { Property = property, FilterOperator = FilterOperator.Contains, FilterValue = "ALI" }
    }.ToFilterString<T>(LogicalFilterOperator.And, FilterCaseSensitivity.CaseInsensitive);

    private sealed class CountReaders : DbCommandInterceptor
    {
        public int Readers { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Readers++;
            return ValueTask.FromResult(result);
        }
    }
}
