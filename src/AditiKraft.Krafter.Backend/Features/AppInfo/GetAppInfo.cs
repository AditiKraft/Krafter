using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.AppInfo;

namespace AditiKraft.Krafter.Backend.Features.AppInfo;

public sealed class GetAppInfo
{
    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder routeGroupBuilder = endpointRouteBuilder.MapGroup(KrafterRoute.AppInfo);
            routeGroupBuilder.MapGet("/", ([FromServices] Handler handler, CancellationToken cancellationToken) =>
            {
                Task<Response<string>> res = handler.GetAppInfoAsync();
                return res;
            });
        }
    }

    public class Handler : IScopedHandler
    {
        public async Task<Response<string>> GetAppInfoAsync()
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

