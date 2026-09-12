using Microsoft.Extensions.Primitives;

namespace AditiKraft.Krafter.Backend.Common.Extensions;

public static class RequestExtensions
{
    public static string GetOrigin(this HttpRequest request)
    {
        string origin = string.Empty;
        if (request.Headers is { Count: > 0 })
        {
            // Prefer Origin header for API/browser requests
            if (request.Headers.TryGetValue("Origin", out StringValues originHeader))
            {
                origin = originHeader.ToString();
            }
            else if (request.Headers.TryGetValue("Referer", out StringValues referer))
            {
                if (Uri.TryCreate(referer.ToString(), UriKind.Absolute, out Uri? refererUri))
                {
                    // Only scheme and host, no port
                    origin = $"{refererUri.Scheme}://{refererUri.Host}";
                }
                else
                {
                    origin = referer.ToString(); // fallback to raw if parsing fails
                }
            }
            else
            {
                origin = request.GetOriginFromRequest();
            }
        }

        if (origin.EndsWith("/"))
        {
            origin = origin.Substring(0, origin.Length - 1);
        }

        return origin;
    }

    private static string GetOriginFromRequest(this HttpRequest request) =>
        $"{request.Scheme}://{request.Host.Value}{request.PathBase.Value}";

    public static string? GetIpAddress(this HttpContext httpContext)
    {
        return httpContext.Request.Headers.ContainsKey("X-Forwarded-For")
            ? httpContext.Request.Headers["X-Forwarded-For"]
            : httpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "N/A";
    }
}
