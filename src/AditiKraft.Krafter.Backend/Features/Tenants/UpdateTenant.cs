using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AditiKraft.Krafter.Backend.Features.Tenants;

public sealed class UpdateTenant
{
    internal sealed class Handler(
        TenantDbContext dbContext,
        IServiceProvider serviceProvider,
        AppUrls urls) : IScopedHandler
    {
        public async Task<Response> UpdateAsync(string id, CreateOrUpdateTenantRequest request,
            CancellationToken cancellationToken)
        {
            request.Id = id;
            Tenant? tenant = await dbContext.Tenants.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (tenant is null)
            {
                return Response.BadRequest("Unable to find tenant, please try again later or contact support.");
            }

            if (tenant.Id == SeedDataConstants.RootTenant.Id)
            {
                if (request.Identifier != tenant.Identifier)
                {
                    return Response.Forbidden("Root tenant identifier cannot be modified.");
                }

                if (request.AdminEmail != tenant.AdminEmail)
                {
                    return Response.Forbidden("Root tenant admin email cannot be modified.");
                }

                if (request.IsActive.HasValue && request.IsActive.Value != tenant.IsActive)
                {
                    return Response.Forbidden("Root tenant cannot be deactivated.");
                }

                if (request.ValidUpto.HasValue && request.ValidUpto.Value != tenant.ValidUpto)
                {
                    return Response.Forbidden("Root tenant validity date cannot be modified.");
                }
            }

            var validation = await new CreateOrUpdateTenantRequestValidator(urls).ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return Response.BadRequest(string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));
            }

            bool identifierExists = await dbContext.Tenants.AsNoTracking()
                .AnyAsync(c => c.Id != tenant.Id && c.Identifier.ToLower() == request.Identifier, cancellationToken);
            if (identifierExists)
            {
                return Response.Conflict("Identifier already exists, please try a different identifier.");
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
                using IServiceScope scope = serviceProvider.CreateScope();

                ITenantSetterService tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantSetterService>();
                CurrentTenantDetails currentTenantDetails = tenant.Adapt<CurrentTenantDetails>();
                currentTenantDetails.TenantLink =
                    TenantLinkBuilder.GetTenantLink(urls, request.Identifier);
                tenantSetter.SetTenant(currentTenantDetails);

                UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                IUserMutationService userService = scope.ServiceProvider.GetRequiredService<IUserMutationService>();

                ApplicationUser? user = await userManager.Users.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.NormalizedEmail == tenant.AdminEmail.ToUpper(), cancellationToken);
                if (user is not null)
                {
                    Response userResult = await userService.UpdateEmailAsync(user.Id, request.AdminEmail, cancellationToken);
                    if (userResult.IsError)
                    {
                        return userResult;
                    }
                }

                tenant.AdminEmail = request.AdminEmail;
            }

            if (request.IsActive != tenant.IsActive)
            {
                tenant.IsActive = request.IsActive ?? tenant.IsActive;
            }

            if (request.ValidUpto != tenant.ValidUpto)
            {
                // Match creation: preserve the selected calendar date at midnight UTC.
                tenant.ValidUpto = DateTime.SpecifyKind(request.ValidUpto!.Value.Date, DateTimeKind.Utc);
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                   { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: TenantDbContext.TenantIdentifierIndexName })
            {
                return Response.Conflict("Identifier already exists, please try a different identifier.");
            }
            return new Response();
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder tenantGroup = endpointRouteBuilder.MapGroup(ApiRoutes.Tenants)
                .AddEndpointFilter((context, next) =>
                {
                    // Validation must use the route ID, not an optional or untrusted body ID.
                    CreateOrUpdateTenantRequest request = context.Arguments.OfType<CreateOrUpdateTenantRequest>().Single();
                    request.Id = context.HttpContext.Request.RouteValues["id"]?.ToString();
                    return next(context);
                })
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
                .MustHavePermission(PermissionAction.Update, PermissionResource.Tenants);
        }
    }
}
