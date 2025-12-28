using Backend.Api;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using Krafter.Shared.Common;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.AppInfo;

namespace Backend.Features.AppInfo;

public sealed class Get
{
    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder routeGroupBuilder = endpointRouteBuilder.MapGroup(KrafterRoute.AppInfo);
            routeGroupBuilder.MapGet("/", ([FromServices] Handler handler, CancellationToken cancellationToken) =>
            {
                Task<Response<string>> res = handler.GetAppInfo();
                return res;
            });
        }
    }

    public class Handler : IScopedHandler
    {
        public async Task<Response<string>> GetAppInfo()
        {
            var res = new Response<string>
            {
                Data =
                    $"Backend version {BuildInfo.Build}, built on {BuildInfo.DateTimeUtc}, running on {RuntimeInformation.FrameworkDescription}"
            };
            return res;
        }
    }
}
