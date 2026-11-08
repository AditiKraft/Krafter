using System.Net;
using System.Net.Http.Json;
using AditiKraft.Krafter.Backend.Features.Roles;
using AditiKraft.Krafter.Backend.Features.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using AditiKraft.Krafter.Tests.Users;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace AditiKraft.Krafter.Tests.Queries;

public sealed class ListQueryEndpointTests
{
    [Theory]
    [InlineData("SkipCount", "-1")]
    [InlineData("MaxResultCount", "-1")]
    [InlineData("MaxResultCount", "0")]
    [InlineData("MaxResultCount", "201")]
    [InlineData("MaxResultCount", "2147483647")]
    [InlineData("OrderBy", "NoSuchProperty")]
    [InlineData("OrderBy", "CreatedOn desc, Id.Length")]
    [InlineData("Filter", "Id ==")]
    [InlineData("Filter", "Id.PadLeft(100) == \"x\"")]
    [InlineData("Filter", "Id.get_Length() > 0")]
    [InlineData("Query", "a\0b")]
    [InlineData("Id", "a\0b")]
    [InlineData("Filter", "Id == \"a\0b\"")]
    [InlineData("Filter", "Id == \"a\\u0000b\"")]
    [InlineData("Filter", "Id == \"a\\uD800b\"")]
    [InlineData("Filter", "Id == \"a\\uDC00b\"")]
    [InlineData("OrderBy", "Id\0")]
    [InlineData("Filter", "(true ? null : \"x\").ToLower() == \"x\"")]
    public async Task ActualGetRoutesRejectInvalidInputs(string name, string value)
    {
        await using var context = new IdentityTestContext();
        await using WebApplication app = await CreateAppAsync(context);
        using var client = Client(app);
        foreach (string path in ListPaths)
        {
            using HttpResponseMessage response = await client.GetAsync($"{path}?{name}={Uri.EscapeDataString(value)}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task RoleMemberRouteRejectsNullCharactersInRoleId()
    {
        await using var context = new IdentityTestContext();
        await using WebApplication app = await CreateAppAsync(context);
        using var client = Client(app);
        using HttpResponseMessage response = await client.GetAsync("/users/by-role/a%00b");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeepConditionalReceiverIsRejectedWithinTheRequestDeadline()
    {
        await using var context = new IdentityTestContext();
        await using WebApplication app = await CreateAppAsync(context);
        using var client = Client(app);
        client.Timeout = TimeSpan.FromSeconds(5);
        string filter = DeepConditionalFilter();
        Assert.True(filter.Length < 2048); // Exercise the tree budget, not the input-length limit.
        using HttpResponseMessage response = await client.GetAsync("/tenants/?Filter=" + Uri.EscapeDataString(filter));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EscapedSurrogatePairRemainsValidUnicodeText()
    {
        await using var context = new IdentityTestContext();
        await using WebApplication app = await CreateAppAsync(context);
        using var client = Client(app);
        string filter = Uri.EscapeDataString("Id == \"a\\uD83D\\uDE00b\"");
        foreach (string path in ListPaths)
        {
            using HttpResponseMessage response = await client.GetAsync(path + "?Filter=" + filter);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    internal static string DeepConditionalFilter()
    {
        string receiver = "Id";
        for (int depth = 0; depth < 80; depth++) receiver = $"(Id != null ? {receiver} : null)";
        return receiver + ".ToLower() == \"x\"";
    }

    [Fact]
    public async Task ActualGetRoutesRejectOversizedTextAndPreserveDefaultPaging()
    {
        await using var context = new IdentityTestContext();
        await using WebApplication app = await CreateAppAsync(context);
        using var client = Client(app);
        foreach (string path in ListPaths)
        {
            using HttpResponseMessage defaultPage = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, defaultPage.StatusCode);
            var result = await defaultPage.Content.ReadFromJsonAsync<Response<PaginationResponse<UserInfo>>>();
            Assert.Equal(10, result!.Data!.MaxResultCount);
            using HttpResponseMessage largestPage = await client.GetAsync($"{path}?MaxResultCount=200");
            Assert.Equal(HttpStatusCode.OK, largestPage.StatusCode);

            foreach ((string name, int length) in new[] { ("Filter", 2049), ("OrderBy", 257), ("Query", 257) })
            {
                using HttpResponseMessage invalid = await client.GetAsync($"{path}?{name}={new string('x', length)}");
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            }
        }
    }

    [Theory]
    [InlineData(FilterOperator.Contains, "AL")]
    [InlineData(FilterOperator.StartsWith, "AL")]
    [InlineData(FilterOperator.EndsWith, "ICE")]
    [InlineData(FilterOperator.Equals, "ALICE")]
    [InlineData(FilterOperator.NotEquals, "BOB")]
    [InlineData(FilterOperator.DoesNotContain, "BOB")]
    public async Task RadzenGeneratedFiltersSupportCaseFoldingAndSorting(FilterOperator operation, string value)
    {
        await using var context = new IdentityTestContext();
        var alice = await context.AddUserAsync("alice@example.test");
        alice.FirstName = "Alice";
        var bob = await context.AddUserAsync("bob@example.test");
        bob.FirstName = "Bob";
        await context.Db.SaveChangesAsync();
        await using WebApplication app = await CreateAppAsync(context);
        using var client = Client(app);
        var filters = new[]
        {
            new CompositeFilterDescriptor { Property = nameof(UserDto.FirstName), FilterOperator = operation, FilterValue = value },
            new CompositeFilterDescriptor { Property = nameof(UserDto.IsActive), FilterOperator = FilterOperator.Equals, FilterValue = true }
        };
        string filter = filters.ToFilterString<UserDto>(LogicalFilterOperator.And, FilterCaseSensitivity.CaseInsensitive);
        var response = await client.GetFromJsonAsync<Response<PaginationResponse<UserDto>>>(
            $"/users/?Filter={Uri.EscapeDataString(filter)}&OrderBy=FirstName%20asc%2C%20CreatedOn%20desc&MaxResultCount=1");

        Assert.False(response!.IsError, response.Message);
        Assert.Equal(alice.Id, Assert.Single(response.Data!.Items).Id);
        Assert.Equal(1, response.Data.TotalCount);
    }

    [Fact]
    public async Task QueryIsLiteralTextAndAllListConsumersKeepPagingAndProjection()
    {
        await using var context = new IdentityTestContext();
        const string literal = "Alice.Contains(\"x\") ==";
        var user = await context.AddUserAsync("actual@example.test");
        user.FirstName = literal;
        var role = await context.AddRoleAsync(literal);
        context.Db.UserRoles.Add(new ApplicationUserRole { UserId = user.Id, RoleId = role.Id });
        await context.Db.SaveChangesAsync();
        context.TenantDb.Tenants.Add(new Tenant
        {
            Id = "blue", Identifier = "blue", Name = literal, AdminEmail = "tenant@example.test",
            IsActive = true, ValidUpto = DateTime.UtcNow.AddDays(1)
        });
        await context.TenantDb.SaveChangesAsync();
        await using WebApplication app = await CreateAppAsync(context);
        using var client = Client(app);
        string query = "?Query=" + Uri.EscapeDataString(literal);

        var users = await client.GetFromJsonAsync<Response<PaginationResponse<UserDto>>>("/users/" + query);
        Assert.Equal(user.Id, Assert.Single(users!.Data!.Items).Id);
        var roles = await client.GetFromJsonAsync<Response<PaginationResponse<RoleDto>>>("/roles/" + query);
        Assert.Equal(role.Id, Assert.Single(roles!.Data!.Items).Id);
        var tenants = await client.GetFromJsonAsync<Response<PaginationResponse<TenantDto>>>("/tenants/" + query);
        Assert.Equal("blue", Assert.Single(tenants!.Data!.Items).Id);
        var members = await client.GetFromJsonAsync<Response<PaginationResponse<UserInfo>>>(
            $"/users/by-role/{role.Id}" + query);
        Assert.Equal(user.Email, Assert.Single(members!.Data!.Items).Email);

        var secondPage = await client.GetFromJsonAsync<Response<PaginationResponse<UserDto>>>(
            "/users/?SkipCount=1&MaxResultCount=1");
        Assert.Empty(secondPage!.Data!.Items);
        Assert.Equal(1, secondPage.Data.TotalCount);
    }

    [Theory]
    [InlineData(FilterOperator.IsNull, FilterCaseSensitivity.Default)]
    [InlineData(FilterOperator.IsNotNull, FilterCaseSensitivity.Default)]
    [InlineData(FilterOperator.IsEmpty, FilterCaseSensitivity.Default)]
    [InlineData(FilterOperator.IsNotEmpty, FilterCaseSensitivity.Default)]
    [InlineData(FilterOperator.IsNull, FilterCaseSensitivity.CaseInsensitive)]
    [InlineData(FilterOperator.IsNotNull, FilterCaseSensitivity.CaseInsensitive)]
    public async Task RadzenNullAndEmptyFiltersRemainSupported(FilterOperator operation,
        FilterCaseSensitivity sensitivity)
    {
        await using var context = new IdentityTestContext();
        var missing = await context.AddUserAsync("missing@example.test");
        missing.FirstName = null;
        var empty = await context.AddUserAsync("empty@example.test");
        empty.FirstName = string.Empty;
        var named = await context.AddUserAsync("named@example.test");
        named.FirstName = "Alice";
        await context.Db.SaveChangesAsync();
        await using WebApplication app = await CreateAppAsync(context);
        using var client = Client(app);
        string filter = new[]
        {
            new CompositeFilterDescriptor { Property = nameof(UserDto.FirstName), FilterOperator = operation }
        }.ToFilterString<UserDto>(LogicalFilterOperator.And, sensitivity);
        var response = await client.GetFromJsonAsync<Response<PaginationResponse<UserDto>>>(
            $"/users/?Filter={Uri.EscapeDataString(filter)}");

        Assert.False(response!.IsError, response.Message);
        string[] ids = response.Data!.Items.Select(user => user.Id).ToArray();
        // Current grids use Default. Radzen 8.3.2 coalesces strings to empty before
        // ToLower in CaseInsensitive mode, including before its null comparison.
        // Preserve the emitted expression's meaning rather than rewriting its AST.
        if (sensitivity == FilterCaseSensitivity.CaseInsensitive)
        {
            if (operation == FilterOperator.IsNull)
                Assert.Empty(ids);
            else
                Assert.Equal(3, ids.Length);
        }
        else if (operation == FilterOperator.IsNull)
            Assert.Equal([missing.Id], ids);
        else if (operation == FilterOperator.IsNotNull)
        {
            Assert.DoesNotContain(missing.Id, ids);
            Assert.Contains(named.Id, ids);
        }
        else if (operation == FilterOperator.IsEmpty)
        {
            Assert.Contains(empty.Id, ids);
            Assert.DoesNotContain(named.Id, ids);
        }
        else
        {
            Assert.Contains(named.Id, ids);
            Assert.DoesNotContain(empty.Id, ids);
        }
    }

    [Fact]
    public async Task NullPropagationFilterKeepsItsMeaning()
    {
        await using var context = new IdentityTestContext();
        var user = await context.AddUserAsync("literal@example.test");
        user.FirstName = "Alice";
        await context.Db.SaveChangesAsync();
        await using WebApplication app = await CreateAppAsync(context);
        using var client = Client(app);
        string filter = Uri.EscapeDataString("np(FirstName, \"\").ToLower().Contains(\"alice\")");
        var response = await client.GetFromJsonAsync<Response<PaginationResponse<UserDto>>>("/users/?Filter=" + filter);
        Assert.Equal(user.Id, Assert.Single(response!.Data!.Items).Id);
    }

    private static readonly string[] ListPaths = ["/users/", "/roles/", "/tenants/", "/users/by-role/unused"];

    private static HttpClient Client(WebApplication app) => new() { BaseAddress = new Uri(Assert.Single(app.Urls)) };

    private static async Task<WebApplication> CreateAppAsync(IdentityTestContext context)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(context.Db);
        builder.Services.AddSingleton(context.TenantDb);
        builder.Services.AddScoped<GetUsers.Handler>();
        builder.Services.AddScoped<GetUsersByRole.Handler>();
        builder.Services.AddScoped<GetRoles.Handler>();
        builder.Services.AddScoped<GetTenants.Handler>();
        builder.Services.AddValidatorsFromAssemblyContaining<GetRequestInputValidator>();
        builder.AddFluentValidationEndpointFilter();
        builder.Services.AddAuthorization(options =>
        {
            foreach (string resource in new[] { PermissionResource.Users, PermissionResource.UserRoles, PermissionResource.Roles, PermissionResource.Tenants })
                options.AddPolicy(PermissionDefinition.NameFor(PermissionAction.View, resource),
                    policy => policy.RequireAssertion(_ => true));
        });
        WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        // Exercise production route binding and filters; authorization has dedicated regressions.
        var routes = app.MapGroup("").AllowAnonymous();
        new GetUsers.Route().MapRoute(routes);
        new GetUsersByRole.Route().MapRoute(routes);
        new GetRoles.Route().MapRoute(routes);
        new GetTenants.Route().MapRoute(routes);
        await app.StartAsync();
        return app;
    }
}
