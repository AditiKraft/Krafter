using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Enums;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AditiKraft.Krafter.Backend.Features.Tenants;

public sealed class CreateTenant
{
    internal sealed class Handler(
        TenantDbContext dbContext,
        IServiceProvider serviceProvider,
        ICurrentUser currentUser,
        AppUrls urls) : IScopedHandler
    {
        public async Task<Response> CreateAsync(CreateOrUpdateTenantRequest request, CancellationToken cancellationToken)
        {
            if (TenantSettings.TenancyMode == TenancyMode.Single)
            {
                return Response.BadRequest("Tenant creation is not allowed in single-tenant mode.");
            }

            request.Id = null;
            var validation = await new CreateOrUpdateTenantRequestValidator(urls).ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return Response.BadRequest(string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));
            }

            if (request.ValidUpto!.Value.Kind != DateTimeKind.Utc)
            {
                return Response.BadRequest("Send the expiry time in UTC with a Z suffix.");
            }

            bool identifierExists = await dbContext.Tenants.AsNoTracking()
                .AnyAsync(c => c.Identifier.ToLower() == request.Identifier, cancellationToken);
            if (identifierExists)
            {
                return Response.Conflict("Identifier already exists, please try a different identifier.");
            }

            request.Id = Guid.NewGuid().ToString();
            Tenant entity = request.Adapt<Tenant>();
            entity.ValidUpto = request.ValidUpto!.Value;
            entity.CreatedOn = DateTime.UtcNow;
            entity.CreatedById = currentUser.GetUserId();

            dbContext.Tenants.Add(entity);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                   { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: TenantDbContext.TenantIdentifierIndexName })
            {
                return Response.Conflict("Identifier already exists, please try a different identifier.");
            }

            using IServiceScope scope = serviceProvider.CreateScope();
            ITenantSetterService tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantSetterService>();
            CurrentTenantDetails currentTenantDetails = entity.Adapt<CurrentTenantDetails>();
            currentTenantDetails.TenantLink =
                TenantLinkBuilder.GetTenantLink(urls, request.Identifier);
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
            RouteGroupBuilder tenantGroup = endpointRouteBuilder.MapGroup(ApiRoutes.Tenants)
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
                .MustHavePermission(PermissionAction.Create, PermissionResource.Tenants);
        }
    }
}
