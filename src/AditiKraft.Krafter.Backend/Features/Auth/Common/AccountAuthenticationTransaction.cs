using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

internal static class AccountAuthenticationTransaction
{
    public static async Task<T> RunAsync<T>(ApplicationDbContext db, string tenantId, string userId,
        Func<Task<T>> action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (db.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("Save pending changes before starting an account authentication transaction.");
        }
        if (!db.Database.IsRelational())
        {
            return await action(); // Unit tests only; concurrency guarantees require PostgreSQL tests.
        }

        // Keep this lock key shared with the privileged account-recovery procedure.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        string key = $"mfa:{tenantId}:{userId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
        // Authentication middleware may have loaded state before another transition committed.
        // This operation owns the unit of work and must reload that state under the account lock.
        db.ChangeTracker.Clear();
        T result = await action();
        await db.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // Return credentials only after commit. Once commit starts, finish independently of
        // request cancellation and do not report a successful commit as canceled.
        await transaction.CommitAsync(CancellationToken.None);
        return result;
    }
}
