using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Common.Interfaces.Auth;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Tenants;

public sealed class CreateTenant
{
    internal sealed class Handler(
        TenantDbContext dbContext,
        KrafterContext krafterContext,
        ITenantGetterService tenantGetterService,
        IServiceProvider serviceProvider,
        ICurrentUser currentUser) : IScopedHandler
    {
        public async Task<Response> CreateAsync(CreateOrUpdateTenantRequest request, CancellationToken cancellationToken)
        {
            request.Id = null;
            if (!string.IsNullOrWhiteSpace(request.Identifier))
            {
                request.Identifier = request.Identifier.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Identifier))
            {
                Tenant? existingTenant = await dbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Identifier.ToLower() == request.Identifier.ToLower(), cancellationToken);
                if (existingTenant is not null)
                {
                    return Response.Conflict("Identifier already exists, please try a different identifier.");
                }
            }

            request.Id = Guid.NewGuid().ToString();
            Tenant entity = request.Adapt<Tenant>();
            entity.ValidUpto = new DateTime(request.ValidUpto!.Value.Year,
                request.ValidUpto.Value.Month, request.ValidUpto.Value.Day, 0, 0, 0, 0, 0,
                DateTimeKind.Utc);
            entity.CreatedById = currentUser.GetUserId();

            dbContext.Tenants.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
            await krafterContext.SaveChangesAsync([nameof(Tenant)], true, cancellationToken);

            string rootTenantLink = tenantGetterService.Tenant.TenantLink;
            using IServiceScope scope = serviceProvider.CreateScope();
            ITenantSetterService tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantSetterService>();
            CurrentTenantDetails currentTenantDetails = entity.Adapt<CurrentTenantDetails>();
            currentTenantDetails.TenantLink =
                TenantLinkBuilder.GetSubTenantLinkBasedOnRootTenant(rootTenantLink, request.Identifier);
            tenantSetter.SetTenant(currentTenantDetails);

            DataSeedService seedService = scope.ServiceProvider.GetRequiredService<DataSeedService>();
            await seedService.SeedBasicData(new SeedDataRequest());

            return new Response();
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder tenantGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Tenants)
                .AddFluentValidationFilter();

            tenantGroup.MapPost("/", async (
                    [FromBody] CreateOrUpdateTenantRequest request,
                    [FromServices] Handler handler,
                    CancellationToken cancellationToken) =>
                {
                    Response res = await handler.CreateAsync(request, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .MustHavePermission(KrafterAction.Create, KrafterResource.Tenants);
        }
    }
}



