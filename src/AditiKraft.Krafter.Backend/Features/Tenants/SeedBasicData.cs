using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using Microsoft.AspNetCore.Mvc;

namespace AditiKraft.Krafter.Backend.Features.Tenants;

public sealed class SeedBasicData
{
    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder tenant = endpointRouteBuilder.MapGroup(KrafterRoute.Tenants).AddFluentValidationFilter();
            tenant.MapPost($"/{RouteSegment.SeedData}", async
                ([FromBody] SeedDataRequest request,
                    [FromServices] DataSeedService tenantSeedService) =>
                {
                    Response res = await tenantSeedService.SeedBasicData(request);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>();
        }
    }
}



