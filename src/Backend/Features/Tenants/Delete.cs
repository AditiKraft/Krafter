using Backend.Api;
using Backend.Api.Authorization;
using Backend.Common;
using Backend.Features.Tenants._Shared;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using Krafter.Shared.Common;
using Krafter.Shared.Common.Auth.Permissions;
using Krafter.Shared.Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Tenants;

public sealed class Delete
{
    internal sealed class Handler(TenantDbContext dbContext, KrafterContext krafterContext) : IScopedHandler
    {
        public async Task<Response> DeleteAsync(DeleteRequestInput requestInput)
        {
            Tenant? tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(c => c.Id == requestInput.Id);
            if (tenant is null)
            {
                return Response.BadRequest(
                    "Unable to find tenant, please try again later or contact support.");
            }

            if (tenant.Id == KrafterInitialConstants.RootTenant.Id)
            {
                return Response.Forbidden(
                    "You cannot delete the root tenant.");
            }

            tenant.IsDeleted = true;
            tenant.DeleteReason = requestInput.DeleteReason;
            dbContext.Tenants.Update(tenant);
            await dbContext.SaveChangesAsync();
            await krafterContext.SaveChangesAsync([nameof(Tenant)]);
            return new Response();
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder tenant = endpointRouteBuilder.MapGroup(KrafterRoute.Tenants).AddFluentValidationFilter();

            tenant.MapPost("/delete", async
                ([FromBody] DeleteRequestInput requestInput,
                    [FromServices] Handler handler) =>
                {
                    Response res = await handler.DeleteAsync(requestInput);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .MustHavePermission(KrafterAction.Delete, KrafterResource.Tenants);
        }
    }
}
