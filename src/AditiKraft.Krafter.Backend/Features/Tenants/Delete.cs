using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Tenants;

public sealed class Delete
{
    internal sealed class Handler(TenantDbContext dbContext, KrafterContext krafterContext) : IScopedHandler
    {
        public async Task<Response> DeleteAsync(string id)
        {
            Tenant? tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
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

            tenant.MapDelete($"/{RouteSegment.ById}", async
                ([FromRoute] string id,
                    [FromServices] Handler handler) =>
                {
                    Response res = await handler.DeleteAsync(id);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .MustHavePermission(KrafterAction.Delete, KrafterResource.Tenants);
        }
    }
}



