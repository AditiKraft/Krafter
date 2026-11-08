using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public sealed class TwoFactorCleanup(IServiceScopeFactory scopes, TimeProvider clock, ILogger<TwoFactorCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using IServiceScope scope = scopes.CreateScope();
                ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                DateTimeOffset cutoff = clock.GetUtcNow().AddDays(-1);
                // Only expired/consumed security state, across tenants. ExecuteDelete deliberately
                // bypasses tracked soft-delete hooks; active credentials are never removed here.
                await db.TwoFactorChallenges.IgnoreQueryFilters().Where(x => x.ExpiresAt < cutoff).ExecuteDeleteAsync(stoppingToken);
                await db.TwoFactorRecoveryCodes.IgnoreQueryFilters().Where(x => x.IsDeleted).ExecuteDeleteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Unable to clean up expired two-factor state."); }
        }
    }
}
