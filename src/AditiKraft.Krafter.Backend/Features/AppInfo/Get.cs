using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using AditiKraft.Krafter.Backend.Api;
using AditiKraft.Krafter.Shared.Common;
using AditiKraft.Krafter.Shared.Common.Models;
using AditiKraft.Krafter.Shared.Contracts.AppInfo;

namespace AditiKraft.Krafter.Backend.Features.AppInfo;

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
                    $"AditiKraft.Krafter.Backend version {BuildInfo.Build}, built on {BuildInfo.DateTimeUtc}, running on {RuntimeInformation.FrameworkDescription}"
            };
            return res;
        }
    }
}
