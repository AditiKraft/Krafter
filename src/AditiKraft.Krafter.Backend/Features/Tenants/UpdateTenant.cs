using AditiKraft.Krafter.Backend.Api;
using AditiKraft.Krafter.Backend.Api.Authorization;
using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Tenants;

public sealed class UpdateTenant
{
    internal sealed class Handler(
        TenantDbContext dbContext,
        KrafterContext krafterContext,
        ITenantGetterService tenantGetterService,
        IServiceProvider serviceProvider) : IScopedHandler
    {
        public async Task<Response> UpdateAsync(string id, CreateOrUpdateTenantRequest request,
            CancellationToken cancellationToken)
        {
            request.Id = id;
            if (!string.IsNullOrWhiteSpace(request.Identifier))
            {
                request.Identifier = request.Identifier.Trim();
            }

            Tenant? tenant = await dbContext.Tenants.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (tenant is null)
            {
                return Response.BadRequest("Unable to find tenant, please try again later or contact support.");
            }

            if (request.Name != tenant.Name)
            {
                tenant.Name = request.Name;
            }

            if (request.Identifier != tenant.Identifier)
            {
                tenant.Identifier = request.Identifier;
            }

            if (request.AdminEmail != tenant.AdminEmail)
            {
                string rootTenantLink = tenantGetterService.Tenant.TenantLink;
                using IServiceScope scope = serviceProvider.CreateScope();

                ITenantSetterService tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantSetterService>();
                CurrentTenantDetails currentTenantDetails = tenant.Adapt<CurrentTenantDetails>();
                currentTenantDetails.TenantLink =
                    TenantLinkBuilder.GetSubTenantLinkBasedOnRootTenant(rootTenantLink, request.Identifier);
                tenantSetter.SetTenant(currentTenantDetails);

                UserManager<KrafterUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<KrafterUser>>();
                IUserService userService = scope.ServiceProvider.GetRequiredService<IUserService>();

                KrafterUser? user = await userManager.Users.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.NormalizedEmail == tenant.AdminEmail.ToUpper(), cancellationToken);
                if (user is not null)
                {
                    await userService.CreateOrUpdateAsync(new CreateUserRequest
                    {
                        Id = user.Id, Email = request.AdminEmail, UpdateTenantEmail = false
                    });
                }

                tenant.AdminEmail = request.AdminEmail;
            }

            if (request.IsActive != tenant.IsActive)
            {
                tenant.IsActive = request.IsActive ?? tenant.IsActive;
            }

            if (request.ValidUpto != tenant.ValidUpto)
            {
                tenant.ValidUpto = request.ValidUpto ?? tenant.ValidUpto;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await krafterContext.SaveChangesAsync([nameof(Tenant)], true, cancellationToken);
            return new Response();
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder tenantGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Tenants)
                .AddFluentValidationFilter();

            tenantGroup.MapPut($"/{RouteSegment.ById}", async (
                    [FromRoute] string id,
                    [FromBody] CreateOrUpdateTenantRequest request,
                    [FromServices] Handler handler,
                    CancellationToken cancellationToken) =>
                {
                    Response res = await handler.UpdateAsync(id, request, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .MustHavePermission(KrafterAction.Update, KrafterResource.Tenants);
        }
    }
}


