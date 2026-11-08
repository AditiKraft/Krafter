using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.AspNetCore.SignalR;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public sealed class SessionHubFilter(IServiceScopeFactory scopes, IHostApplicationLifetime lifetime, ILogger<SessionHubFilter> logger) : IHubFilter
{
    private const string MonitorKey = "Krafter.SessionMonitor";

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (!await ValidAsync(context.Context))
        {
            context.Context.Abort();
            throw new HubException("Session expired. Sign in again.");
        }
        return await next(context);
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        if (!await ValidAsync(context.Context))
        {
            context.Context.Abort();
            return;
        }
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.Context.ConnectionAborted, lifetime.ApplicationStopping);
        context.Context.Items[MonitorKey] = (cancellation, WatchAsync(context.Context, cancellation.Token));
        await next(context);
    }

    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        if (context.Context.Items.Remove(MonitorKey, out object? value) && value is ValueTuple<CancellationTokenSource, Task> monitor)
        {
            await monitor.Item1.CancelAsync();
            try
            {
                await monitor.Item2;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                monitor.Item1.Dispose();
            }
        }
        await next(context, exception);
    }

    private async Task WatchAsync(HubCallerContext context, CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellation))
            {
                if (!await ValidAsync(context))
                {
                    context.Abort();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
    }

    private async Task<bool> ValidAsync(HubCallerContext context)
    {
        try
        {
            HttpContext? http = context.GetHttpContext();
            if (http is null || context.User is null ||
                !long.TryParse(context.User.FindFirst("exp")?.Value, out long expires) || expires <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                return false;
            }
            // Long Polling clones HttpContext with a fresh service scope. Items retains the
            // trusted tenant resolved by middleware; the new scoped service starts at root.
            CurrentTenantDetails original = http.Items[CurrentTenantService.ResolvedTenantItemKey] as CurrentTenantDetails
                ?? http.RequestServices.GetRequiredService<ITenantGetterService>().Tenant;
            using IServiceScope scope = scopes.CreateScope();
            ITenantFinderService finder = scope.ServiceProvider.GetRequiredService<ITenantFinderService>();
            var found = await finder.Find(original.Identifier);
            if (found.IsError || found.Data is null || found.Data.Id != original.Id)
            {
                return false;
            }

            scope.ServiceProvider.GetRequiredService<ITenantSetterService>().SetTenant(new CurrentTenantDetails
            {
                Id = found.Data.Id,
                Identifier = found.Data.Identifier,
                IsActive = found.Data.IsActive,
                IsDeleted = found.Data.IsDeleted,
                ValidUpto = found.Data.ValidUpto,
                TenantLink = original.TenantLink
            });
            if (!await scope.ServiceProvider.GetRequiredService<SessionValidator>().ValidateAsync(context.User))
            {
                return false;
            }
            // RealtimeHub also uses this HTTP scope when joining its tenant group.
            http.RequestServices.GetRequiredService<ITenantSetterService>().SetTenant(original);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Realtime session validation failed; closing the connection.");
            return false;
        }
    }
}
