using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace AditiKraft.Krafter.Tests.Auth;

[Collection("Google OAuth PostgreSQL")]
public sealed class GoogleOAuthPostgresTests
{
    [OAuthPostgresFact]
    public async Task SeparateReplicasConsumeTheProtectedTransactionOnce()
    {
        var clock = new MfaClock();
        var keys = new EphemeralDataProtectionProvider();
        string proof = GoogleLoginTransactionStore.NewSecret();
        string tenant = Guid.NewGuid().ToString();
        string id;
        await using (TenantDbContext db = Context())
        {
            await db.Database.MigrateAsync();
            var store = new GoogleLoginTransactionStore(db, keys, clock);
            id = await store.CreateAsync("authorize", tenant, GoogleLoginTransactionStore.Hash(proof),
                new Payload("secret-pkce-verifier"), clock.GetUtcNow().AddMinutes(5), default);
            GoogleLoginTransaction row = await db.GoogleLoginTransactions.SingleAsync(x => x.Id == GoogleLoginTransactionStore.Hash(id));
            Assert.DoesNotContain("secret-pkce-verifier", row.ProtectedPayload);
        }
        async Task<Payload?> Consume(string givenProof, string givenTenant, IDataProtectionProvider protector)
        {
            await using TenantDbContext db = Context();
            return await new GoogleLoginTransactionStore(db, protector, clock).TakeAsync<Payload>(id, "authorize", givenProof, givenTenant, default);
        }
        Assert.Null(await Consume(GoogleLoginTransactionStore.NewSecret(), tenant, keys));
        Assert.Null(await Consume(proof, "wrong-tenant", keys));
        Assert.Null(await Consume(proof, tenant, new EphemeralDataProtectionProvider()));
        Payload?[] attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Consume(proof, tenant, keys)));
        Assert.Single(attempts, x => x is not null);
        Assert.Null(await Consume(proof, tenant, keys));
    }

    [OAuthPostgresFact]
    public async Task ExpiredHandoffCannotReturnCredentials()
    {
        var clock = new MfaClock();
        await using TenantDbContext db = Context();
        await db.Database.MigrateAsync();
        var store = new GoogleLoginTransactionStore(db, new EphemeralDataProtectionProvider(), clock);
        string nonce = GoogleLoginTransactionStore.NewSecret();
        string id = await store.CreateAsync("handoff", "tenant", GoogleLoginTransactionStore.Hash(nonce),
            new Payload("credential-envelope"), clock.GetUtcNow().AddMinutes(1), default);
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Null(await store.TakeAsync<Payload>(id, "handoff", nonce, "tenant", default));
    }

    [OAuthPostgresFact]
    public async Task ExpiryWhileTheAtomicDeleteWaitsCannotReleaseItsPayload()
    {
        var clock = new MfaClock();
        var keys = new EphemeralDataProtectionProvider();
        string nonce = GoogleLoginTransactionStore.NewSecret();
        string id;
        await using (TenantDbContext setup = Context())
        {
            await setup.Database.MigrateAsync();
            id = await new GoogleLoginTransactionStore(setup, keys, clock).CreateAsync("handoff", "tenant",
                GoogleLoginTransactionStore.Hash(nonce), new Payload("must-not-be-returned"), clock.GetUtcNow().AddMinutes(1), default);
        }
        var interceptor = new AdvanceAfterConsumption(clock);
        await using TenantDbContext consumer = Context(interceptor);
        Payload? result = await new GoogleLoginTransactionStore(consumer, keys, clock)
            .TakeAsync<Payload>(id, "handoff", nonce, "tenant", default);
        Assert.True(interceptor.Advanced);
        Assert.Null(result);
        Assert.False(await consumer.GoogleLoginTransactions.AnyAsync(x => x.Id == GoogleLoginTransactionStore.Hash(id)));
    }

    private static TenantDbContext Context(IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("KRAFTER_TEST_OAUTH_POSTGRES")!);
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options);
    }

    private sealed class AdvanceAfterConsumption(MfaClock clock) : DbCommandInterceptor
    {
        public bool Advanced { get; private set; }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (result == 1 && command.CommandText.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("GoogleLoginTransactions", StringComparison.Ordinal))
            { clock.Advance(TimeSpan.FromMinutes(2)); Advanced = true; }
            return ValueTask.FromResult(result);
        }
    }
    public sealed record Payload(string Value);
}

[CollectionDefinition("Google OAuth PostgreSQL", DisableParallelization = true)]
public sealed class GoogleOAuthPostgresCollection;

public sealed class OAuthPostgresFactAttribute : FactAttribute
{
    public OAuthPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KRAFTER_TEST_OAUTH_POSTGRES")))
            Skip = "Set KRAFTER_TEST_OAUTH_POSTGRES to a dedicated OAuth integration-test database.";
    }
}
