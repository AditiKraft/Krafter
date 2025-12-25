using Microsoft.AspNetCore.Http;

namespace Krafter.UI.Web.Client.Infrastructure.Services;

public class NullHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; } = null;
}
