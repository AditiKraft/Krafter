using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace AditiKraft.Krafter.Backend.Infrastructure.Persistence;

// Owns one unit of work, including Identity managers that save before returning.
// Both contexts use appDb; a participant borrows the owner's connection only for this transaction.
internal sealed class DomainTransaction : IAsyncDisposable
{
    private readonly DbContext _owner;
    private readonly DbContext? _participant;
    private readonly IDbContextTransaction? _transaction;
    private readonly IDbContextTransaction? _enlistment;
    private readonly string? _participantConnectionString;
    private bool _committed;

    private DomainTransaction(DbContext owner, DbContext? participant,
        IDbContextTransaction? transaction, IDbContextTransaction? enlistment, string? participantConnectionString = null)
        => (_owner, _participant, _transaction, _enlistment, _participantConnectionString) =
            (owner, participant, transaction, enlistment, participantConnectionString);

    public static async Task<DomainTransaction> BeginAsync(DbContext owner,
        CancellationToken cancellationToken, DbContext? participant = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (owner.ChangeTracker.HasChanges() || participant?.ChangeTracker.HasChanges() == true)
            throw new InvalidOperationException("Save pending changes before starting a domain transaction.");
        if (!owner.Database.IsRelational())
        {
            // InMemory is used only by unit tests; it provides no rollback guarantees.
            if (participant?.Database.IsRelational() == true)
                throw new InvalidOperationException("A domain transaction requires matching database providers.");
            return new DomainTransaction(owner, participant, null, null);
        }

        if (owner.Database.CurrentTransaction is not null || participant?.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("A domain transaction must own its unit of work.");

        if (owner.Database.GetDbConnection().State != ConnectionState.Closed)
            throw new InvalidOperationException("A domain transaction requires an idle connection.");

        string? participantConnectionString = null;
        if (participant is not null)
        {
            if (!participant.Database.IsRelational() || owner.Database.ProviderName != participant.Database.ProviderName)
                throw new InvalidOperationException("A domain transaction requires matching database providers.");
            var connection = owner.Database.GetDbConnection();
            var other = participant.Database.GetDbConnection();
            if (connection.DataSource != other.DataSource || connection.Database != other.Database ||
                other.State != ConnectionState.Closed)
                throw new InvalidOperationException("Domain contexts must use the same database and an idle connection.");
            // appDb contexts are configured from a connection string. Externally owned connections
            // have a different lifetime; leave them untouched instead of silently taking ownership.
            if (participant.GetService<IDbContextOptions>().Extensions.OfType<RelationalOptionsExtension>()
                    .Any(options => options.Connection is not null))
                throw new InvalidOperationException("The participant must use a context-owned connection.");
            participantConnectionString = participant.Database.GetConnectionString()
                ?? throw new InvalidOperationException("The participant requires a connection string.");
        }

        IDbContextTransaction transaction = await owner.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            participant?.Database.SetDbConnection(owner.Database.GetDbConnection(), contextOwnsConnection: false);
            IDbContextTransaction? enlistment = participant is null ? null :
                await participant.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);
            return new DomainTransaction(owner, participant, transaction, enlistment, participantConnectionString);
        }
        catch
        {
            await transaction.DisposeAsync();
            RestoreConnection(participant, participantConnectionString);
            throw;
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_transaction is not null)
            await _transaction.CommitAsync(CancellationToken.None);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_committed && _transaction is not null)
                await _transaction.RollbackAsync(CancellationToken.None);
        }
        finally
        {
            if (!_committed)
            {
                _owner.ChangeTracker.Clear();
                _participant?.ChangeTracker.Clear();
            }
            if (_enlistment is not null)
                await _enlistment.DisposeAsync();
            if (_transaction is not null)
                await _transaction.DisposeAsync();
            RestoreConnection(_participant, _participantConnectionString);
        }
    }

    private static void RestoreConnection(DbContext? participant, string? connectionString)
    {
        if (participant is null || connectionString is null)
            return;
        participant.Database.SetDbConnection(null, contextOwnsConnection: true);
        participant.Database.SetConnectionString(connectionString);
    }
}
