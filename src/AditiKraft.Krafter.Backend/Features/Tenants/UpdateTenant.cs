using AditiKraft.Krafter.Backend.Common.Auth;
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
        AppUrls urls,
        ICurrentUser currentUser) : IScopedHandler
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

            if (request.ValidUpto!.Value.Kind != DateTimeKind.Utc
                && !(tenant.Id == DefaultTenantConstants.Identifier && request.ValidUpto == DateTime.MaxValue))
            {
                return Response.BadRequest("Send the expiry time in UTC with a Z suffix.");
            }

            bool identifierExists = await dbContext.Tenants.AsNoTracking()
                .AnyAsync(c => c.Id != tenant.Id && c.Identifier.ToLower() == request.Identifier, cancellationToken);
            if (identifierExists)
            {
                return Response.Conflict("Identifier already exists, please try a different identifier.");
            }

            bool changesAdminEmail = request.AdminEmail != tenant.AdminEmail;
            using IServiceScope? scope = changesAdminEmail ? serviceProvider.CreateScope() : null;
            ApplicationDbContext? identity = null;
            IUserMutationService? userService = null;
            string? ownerId = null;
            if (scope is not null)
            {
                if (!string.IsNullOrWhiteSpace(currentUser.GetUserId()))
                    scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUserId(currentUser.GetUserId());
                ITenantSetterService tenantSetter = scope.ServiceProvider.GetRequiredService<ITenantSetterService>();
                CurrentTenantDetails currentTenantDetails = tenant.Adapt<CurrentTenantDetails>();
                currentTenantDetails.TenantLink = TenantLinkBuilder.GetTenantLink(urls, request.Identifier);
                tenantSetter.SetTenant(currentTenantDetails);
                identity = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                Response identityValidation = UserIdentityValidation.Validate(userManager, request.AdminEmail, request.AdminEmail);
                if (identityValidation.IsError)
                    return identityValidation;

                // The immutable owner account identifies the target even if an older failed update
                // left the registry email stale. Missing or ambiguous ownership must not report success.
                List<string> ownerIds = await userManager.Users.AsNoTracking().Where(user => user.IsOwner)
                    .Select(user => user.Id).Take(2).ToListAsync(cancellationToken);
                if (ownerIds.Count != 1)
                    return Response.Conflict("The tenant must have exactly one owner before its admin email can be changed.");
                ownerId = ownerIds[0];
                userService = scope.ServiceProvider.GetRequiredService<IUserMutationService>();
            }

            await using var transaction = await DomainTransaction.BeginAsync(dbContext, cancellationToken, identity);
            try
            {
                if (changesAdminEmail)
                {
                    Response userResult = await userService!.UpdateEmailAsync(ownerId!, request.AdminEmail, cancellationToken);
                    if (userResult.IsError)
                        return userResult;
                }

                tenant.Name = request.Name;
                tenant.Identifier = request.Identifier;
                tenant.AdminEmail = request.AdminEmail;
                tenant.IsActive = request.IsActive ?? tenant.IsActive;
                tenant.ValidUpto = request.ValidUpto!.Value;
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
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
