using System.Text.Json;
using Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Auth;

public static class AuthApiErrors
{
    public static async Task<Response> CallAsync(Func<Task<Response>> call, CancellationToken cancellationToken = default)
    {
        // Reuse transport handling for management responses that intentionally have no Data field.
        Response<Response> result = await CallAsync(async () =>
        {
            Response? response = await call();
            return response is null
                ? Response<Response>.CustomError("Invalid server response. Please sign in again.", 502)
                : Response<Response>.Success(response);
        }, cancellationToken);
        return result.IsError ? new Response
        { IsError = true, StatusCode = result.StatusCode, Message = result.Message, Error = result.Error } : result.Data!;
    }

    public static async Task<Response<T>> CallAsync<T>(Func<Task<Response<T>>> call, CancellationToken cancellationToken = default)
    {
        try
        {
            Response<T>? response = await call();
            return response is null || (!response.IsError && response.Data is null)
                ? Response<T>.CustomError("Invalid server response. Please sign in again.", 502)
                : response;
        }
        catch (ApiException ex)
        {
            // Preserve useful server feedback without logging secret-bearing request/response payloads.
            try
            {
                if (!string.IsNullOrEmpty(ex.Content))
                {
                    Response<T>? result = JsonSerializer.Deserialize<Response<T>>(ex.Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (result?.IsError == true)
                    {
                        return result;
                    }
                }
            }
            catch (JsonException)
            {
            }
            int status = (int)ex.StatusCode;
            return Response<T>.CustomError("Unable to complete authentication. Please try again.", status is >= 200 and < 300 ? 502 : status);
        }
        catch (JsonException)
        {
            return Response<T>.CustomError("Invalid server response. Please sign in again.", 502);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Response<T>.CustomError("Authentication timed out. Please try again.", 503);
        }
        catch (HttpRequestException)
        {
            return Response<T>.CustomError("Unable to reach the server. Please try again.", 503);
        }
    }
}
