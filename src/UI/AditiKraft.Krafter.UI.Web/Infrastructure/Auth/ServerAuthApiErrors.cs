using Polly;

namespace AditiKraft.Krafter.UI.Web.Infrastructure.Auth;

public static class ServerAuthApiErrors
{
    public const string UnavailableMessage = "Authentication service is temporarily unavailable. Please try again.";

    public static async Task<Response> CallAsync(Func<Task<Response>> call, CancellationToken cancellationToken = default)
    {
        try
        {
            return await AuthApiErrors.CallAsync(call, cancellationToken);
        }
        catch (ExecutionRejectedException)
        {
            return Response.CustomError(UnavailableMessage, StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static async Task<Response<T>> CallAsync<T>(Func<Task<Response<T>>> call, CancellationToken cancellationToken = default)
    {
        try
        {
            return await AuthApiErrors.CallAsync(call, cancellationToken);
        }
        catch (ExecutionRejectedException)
        {
            // Standard server HTTP resilience can reject on timeout, open circuit, or overload.
            return Response<T>.CustomError(UnavailableMessage, StatusCodes.Status503ServiceUnavailable);
        }
    }
}
