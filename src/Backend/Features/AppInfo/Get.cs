using Backend.Api;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using Krafter.Shared.Common;
using Krafter.Shared.Common.Models;

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
                    $"Backend version {Krafter.Shared.Features.AppInfo.Get.BuildInfo.Build}, built on {Krafter.Shared.Features.AppInfo.Get.BuildInfo.DateTimeUtc}, running on {RuntimeInformation.FrameworkDescription}"
            };
            return res;
        }
    }
}
