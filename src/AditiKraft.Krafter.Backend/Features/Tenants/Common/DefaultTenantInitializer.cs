using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Enums;
using AditiKraft.Krafter.Contracts.Common.Models;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Tenants.Common;

// StartAsync is awaited before the web host accepts requests in either hosting variant.
public sealed class DefaultTenantInitializer(IServiceScopeFactory scopeFactory, AppUrls urls) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        Tenant tenant = TenantSettings.TenancyMode == TenancyMode.Single
            ? SeedDataConstants.DefaultTenant
            : await scope.ServiceProvider.GetRequiredService<TenantDbContext>().Tenants.AsNoTracking()
                .SingleOrDefaultAsync(tenant => tenant.Id == SeedDataConstants.RootTenant.Id, cancellationToken)
                ?? throw new InvalidOperationException("The default tenant is missing. Apply database migrations before starting the application.");
        CurrentTenantDetails current = tenant.Adapt<CurrentTenantDetails>();
        current.TenantLink = urls.GetRootUiUri().GetLeftPart(UriPartial.Authority);
        scope.ServiceProvider.GetRequiredService<ITenantSetterService>().SetTenant(current);

        await scope.ServiceProvider.GetRequiredService<DataSeedService>().SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
