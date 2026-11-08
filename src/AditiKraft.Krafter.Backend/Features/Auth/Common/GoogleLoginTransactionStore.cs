using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public interface IGoogleLoginTransactionStore
{
    Task<string> CreateAsync<T>(string purpose, string tenantId, string proofHash, T payload,
        DateTimeOffset expiresAt, CancellationToken cancellationToken);
    Task<T?> TakeAsync<T>(string id, string purpose, string proof, string? tenantId,
        CancellationToken cancellationToken) where T : class;
}

public sealed class GoogleLoginTransactionStore(TenantDbContext db, IDataProtectionProvider protection,
    TimeProvider clock) : IGoogleLoginTransactionStore
{
    public static string NewSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static bool IsSecret(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    public async Task<string> CreateAsync<T>(string purpose, string tenantId, string proofHash, T payload,
        DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        await db.GoogleLoginTransactions.Where(x => x.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
        string id = NewSecret();
        var row = new GoogleLoginTransaction
        {
            Id = Hash(id), TenantId = tenantId, Purpose = purpose, BrowserProofHash = proofHash,
            ProtectedPayload = Protector(purpose, tenantId, Hash(id)).Protect(JsonSerializer.Serialize(payload)), ExpiresAt = expiresAt
        };
        db.GoogleLoginTransactions.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        db.Entry(row).State = EntityState.Detached;
        return id;
    }

    public async Task<T?> TakeAsync<T>(string id, string purpose, string proof, string? tenantId,
        CancellationToken cancellationToken) where T : class
    {
        if (!IsSecret(id) || !IsSecret(proof)) return null;
        string key = Hash(id);
        string proofHash = Hash(proof);
        DateTimeOffset now = clock.GetUtcNow();
        IQueryable<GoogleLoginTransaction> matching = db.GoogleLoginTransactions.Where(x =>
            x.Id == key && x.Purpose == purpose && x.BrowserProofHash == proofHash && x.ExpiresAt > now &&
            (tenantId == null || x.TenantId == tenantId));
        GoogleLoginTransaction? row = await matching.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        T? payload;
        try
        {
            payload = JsonSerializer.Deserialize<T>(Protector(row.Purpose, row.TenantId, row.Id).Unprotect(row.ProtectedPayload));
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            // Missing/rotated-away keys and corrupted state fail closed without exposing payloads.
            return null;
        }
        if (payload is null) return null;
        // This conditional SQL DELETE is the shared, atomic consumption boundary across replicas.
        // Wrong proof/tenant never deletes the row. A concurrent loser never receives its payload.
        now = clock.GetUtcNow();
        int consumed = await matching.ExecuteDeleteAsync(cancellationToken);
        // The database command can wait on a row lock. Recheck after that wait before releasing any proof/credentials.
        return consumed == 1 && row.ExpiresAt > clock.GetUtcNow() ? payload : null;
    }

    private IDataProtector Protector(string purpose, string tenantId, string id) =>
        protection.CreateProtector("Krafter.GoogleLogin.v1", purpose, tenantId, id);
}
