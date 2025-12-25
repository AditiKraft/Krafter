using Backend.Api;
using Backend.Common;
using Backend.Common.Models;
using Backend.Features.Tenants._Shared;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Features.Tenants;

public sealed class SeedBasicData
{
    public sealed class SeedDataRequestInput
    {
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder tenant = endpointRouteBuilder.MapGroup(KrafterRoute.Tenants).AddFluentValidationFilter();
            tenant.MapPost("/seed-data", async
                    ([FromBody] SeedDataRequestInput requestInput, [FromServices] DataSeedService tenantSeedService) =>
                {
                    Response res = await tenantSeedService.SeedBasicData(requestInput);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Common.Models.Response>();
        }
    }
}
