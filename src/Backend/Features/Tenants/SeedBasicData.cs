using Backend.Api;
using Backend.Features.Tenants._Shared;
using Krafter.Shared.Common;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Tenants;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Features.Tenants;

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
