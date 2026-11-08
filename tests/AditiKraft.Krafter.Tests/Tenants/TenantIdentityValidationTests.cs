using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using AditiKraft.Krafter.Tests.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Tenants;

public sealed class TenantIdentityValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerUsernameValidationFollowsConfiguredIdentityAlphabet(bool allowApostrophe)
    {
        var services = new ServiceCollection();
        DomainTestServices.Register(services, "root");
        if (allowApostrophe)
            services.Configure<IdentityOptions>(options => options.User.AllowedUserNameCharacters += "'");
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var request = Request("o'brien@example.com");
        var registry = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        var handler = new CreateTenant.Handler(registry, scope.ServiceProvider,
            scope.ServiceProvider.GetRequiredService<ICurrentUser>(), DomainTestServices.Urls);

        var result = await handler.CreateAsync(request, CancellationToken.None);

        Assert.Equal(allowApostrophe ? 200 : 400, result.StatusCode);
        Assert.Equal(allowApostrophe ? 1 : 0, await registry.Tenants.CountAsync());
        Assert.Equal(allowApostrophe ? 1 : 0, provider.GetRequiredService<DomainJobs>().Emails.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task TenantEmailChangesTargetTheOwnerAndRejectMissingOrAmbiguousOwnership(int owners)
    {
        string tenantId = Guid.NewGuid().ToString();
        await using var provider = DomainTestServices.Provider(tenantId);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registry = services.GetRequiredService<TenantDbContext>();
        registry.Tenants.Add(new Tenant
        { Id = tenantId, Identifier = "blue", Name = "Before", AdminEmail = "stale.registry@example.com", IsActive = true });
        await registry.SaveChangesAsync();
        for (int index = 0; index < owners; index++)
            await DomainTestServices.AddUserAsync(services, $"owner{index}@example.com", owner: true);
        var handler = new UpdateTenant.Handler(registry, services, DomainTestServices.Urls,
            services.GetRequiredService<ICurrentUser>());

        var result = await handler.UpdateAsync(tenantId, Request("final@example.com"), CancellationToken.None);

        Assert.Equal(owners == 1 ? 200 : 409, result.StatusCode);
        await registry.SaveChangesAsync();
        registry.ChangeTracker.Clear();
        Assert.Equal(owners == 1 ? "final@example.com" : "stale.registry@example.com", (await registry.Tenants.SingleAsync()).AdminEmail);
        await using var fresh = provider.CreateAsyncScope();
        var db = fresh.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (owners == 1) Assert.Equal("final@example.com", (await db.Users.SingleAsync()).Email);
        else Assert.DoesNotContain(await db.Users.ToListAsync(), user => user.Email == "final@example.com");
    }

    [Fact]
    public void TenantEmailLengthUsesTheIdentityStorageLimit()
    {
        var validator = new CreateOrUpdateTenantRequestValidator(DomainTestServices.Urls);
        Assert.True(validator.Validate(Request(new string('a', 244) + "@example.com")).IsValid);
        Assert.False(validator.Validate(Request(new string('a', 245) + "@example.com")).IsValid);
    }

    private static CreateOrUpdateTenantRequest Request(string email) => new()
    { Identifier = "blue", Name = "After", AdminEmail = email, IsActive = true, ValidUpto = DateTime.UtcNow.AddDays(30) };
}
