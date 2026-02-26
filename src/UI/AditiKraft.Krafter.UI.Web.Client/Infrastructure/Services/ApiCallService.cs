using System.Net;
using System.Text.Json;
using Refit;

namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;

public class ApiCallService(NotificationService notificationService)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<Response<T>> CallAsync<T>(
        Func<Task<Response<T>>> apiCall,
        string? errorTitle = null,
        string? errorMessage = null,
        string? successMessage = null,
        string? successTitle = null,
        bool showErrorNotification = true)
    {
        try
        {
            Response<T> response = await apiCall();

            if (response.IsError && showErrorNotification)
            {
                string message = GetErrorMessage(errorMessage, response.Message, response.Error);
                notificationService.Notify(NotificationSeverity.Error, errorTitle ?? "Error", message);
            }
            else if (!response.IsError && !string.IsNullOrEmpty(successMessage))
            {
                notificationService.Notify(NotificationSeverity.Success, successTitle ?? "Success", successMessage);
            }

            return response;
        }
        catch (ApiException ex)
        {
            return await HandleApiExceptionAsync<T>(ex, errorTitle, errorMessage, showErrorNotification);
        }
        catch (HttpRequestException ex)
        {
            return HandleHttpException<T>(ex, errorTitle, errorMessage, showErrorNotification);
        }
        catch (Exception ex)
        {
            return HandleGenericException<T>(ex, errorTitle, errorMessage, showErrorNotification);
        }
    }


    public async Task<Response> CallAsync(
        Func<Task<Response>> apiCall,
        string? errorTitle = null,
        string? errorMessage = null,
        string? successMessage = null,
        string? successTitle = null,
        bool showErrorNotification = true)
    {
        try
        {
            Response response = await apiCall();

            if (response.IsError && showErrorNotification)
            {
                string message = GetErrorMessage(errorMessage, response.Message, response.Error);
                notificationService.Notify(NotificationSeverity.Error, errorTitle ?? "Error", message);
            }
            else if (!response.IsError && !string.IsNullOrEmpty(successMessage))
            {
                notificationService.Notify(NotificationSeverity.Success, successTitle ?? "Success", successMessage);
            }

            return response;
        }
        catch (ApiException ex)
        {
            return await HandleApiExceptionAsync(ex, errorTitle, errorMessage, showErrorNotification);
        }
        catch (HttpRequestException ex)
        {
            return HandleHttpException(ex, errorTitle, errorMessage, showErrorNotification);
        }
        catch (Exception ex)
        {
            return HandleGenericException(ex, errorTitle, errorMessage, showErrorNotification);
        }
    }

    private async Task<Response<T>> HandleApiExceptionAsync<T>(ApiException ex, string? errorTitle,
        string? errorMessage, bool showNotification)
    {
        (string message, ErrorResult errorResult) = await ParseApiErrorAsync(ex, errorMessage);

        if (showNotification)
        {
            notificationService.Notify(NotificationSeverity.Error, errorTitle ?? "Error", message);
        }

        return new Response<T>
        {
            IsError = true, StatusCode = (int)ex.StatusCode, Message = message, Error = errorResult
        };
    }

    private async Task<Response> HandleApiExceptionAsync(ApiException ex, string? errorTitle, string? errorMessage,
        bool showNotification)
    {
        (string message, ErrorResult errorResult) = await ParseApiErrorAsync(ex, errorMessage);

        if (showNotification)
        {
            notificationService.Notify(NotificationSeverity.Error, errorTitle ?? "Error", message);
        }

        return new Response { IsError = true, StatusCode = (int)ex.StatusCode, Message = message, Error = errorResult };
    }


    private async Task<(string Message, ErrorResult Error)> ParseApiErrorAsync(ApiException ex,
        string? customErrorMessage)
    {
        if (!string.IsNullOrEmpty(customErrorMessage))
        {
            return (customErrorMessage, new ErrorResult { Message = customErrorMessage });
        }

        string? content = ex.Content;
        if (string.IsNullOrEmpty(content))
        {
            string fallback = GetHttpStatusMessage(ex.StatusCode);
            return (fallback, new ErrorResult { Message = fallback });
        }

        try
        {
            // Try to parse as Response (our standard error format)
            Response? errorResponse = JsonSerializer.Deserialize<Response>(content, JsonOptions);
            if (errorResponse is { IsError: true } ||
                (errorResponse?.StatusCode != 0 && errorResponse?.StatusCode != 200))
            {
                string message = GetErrorMessage(null, errorResponse?.Message, errorResponse?.Error);
                return (message, errorResponse?.Error ?? new ErrorResult { Message = message });
            }

            // Try to parse as ValidationErrorResponse (FluentValidation format)
            ValidationErrorResponse? validationError =
                JsonSerializer.Deserialize<ValidationErrorResponse>(content, JsonOptions);
            if (validationError?.Errors?.Count > 0)
            {
                var messages = new List<string>();
                foreach (KeyValuePair<string, List<string>> error in validationError.Errors)
                {
                    messages.AddRange(error.Value.Select(v => $"{error.Key}: {v}"));
                }

                string validationMessage = string.Join("\n", messages);
                return (validationMessage, new ErrorResult { Message = validationError.Title, Messages = messages });
            }
        }
        catch
        {
            // JSON parsing failed, use content as-is or fallback
        }

        string finalMessage = !string.IsNullOrWhiteSpace(content) ? content : GetHttpStatusMessage(ex.StatusCode);
        return (finalMessage, new ErrorResult { Message = finalMessage });
    }


    private Response<T> HandleHttpException<T>(HttpRequestException ex, string? errorTitle, string? errorMessage,
        bool showNotification)
    {
        string message = errorMessage ?? "Unable to connect to the server. Please check your connection.";

        if (showNotification)
        {
            notificationService.Notify(NotificationSeverity.Error, errorTitle ?? "Connection Error", message);
        }

        return new Response<T>
        {
            IsError = true,
            StatusCode = (int)HttpStatusCode.ServiceUnavailable,
            Message = message,
            Error = new ErrorResult { Message = ex.Message }
        };
    }

    private Response HandleHttpException(HttpRequestException ex, string? errorTitle, string? errorMessage,
        bool showNotification)
    {
        string message = errorMessage ?? "Unable to connect to the server. Please check your connection.";

        if (showNotification)
        {
            notificationService.Notify(NotificationSeverity.Error, errorTitle ?? "Connection Error", message);
        }

        return new Response
        {
            IsError = true,
            StatusCode = (int)HttpStatusCode.ServiceUnavailable,
            Message = message,
            Error = new ErrorResult { Message = ex.Message }
        };
    }

    private Response<T> HandleGenericException<T>(Exception ex, string? errorTitle, string? errorMessage,
        bool showNotification)
    {
        string message = errorMessage ?? "An unexpected error occurred. Please try again.";

        if (showNotification)
        {
            notificationService.Notify(NotificationSeverity.Error, errorTitle ?? "Error", message);
        }

        return new Response<T>
        {
            IsError = true,
            StatusCode = (int)HttpStatusCode.InternalServerError,
            Message = message,
            Error = new ErrorResult { Message = ex.Message }
        };
    }

    private Response HandleGenericException(Exception ex, string? errorTitle, string? errorMessage,
        bool showNotification)
    {
        string message = errorMessage ?? "An unexpected error occurred. Please try again.";

        if (showNotification)
        {
            notificationService.Notify(NotificationSeverity.Error, errorTitle ?? "Error", message);
        }

        return new Response
        {
            IsError = true,
            StatusCode = (int)HttpStatusCode.InternalServerError,
            Message = message,
            Error = new ErrorResult { Message = ex.Message }
        };
    }


    private static string GetHttpStatusMessage(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => "You are not authorized. Please log in again.",
            HttpStatusCode.Forbidden => "You don't have permission to perform this action.",
            HttpStatusCode.NotFound => "The requested resource was not found.",
            HttpStatusCode.BadRequest => "Invalid request. Please check your input.",
            HttpStatusCode.InternalServerError => "Server error. Please try again later.",
            HttpStatusCode.ServiceUnavailable => "Service temporarily unavailable. Please try again later.",
            _ => "An error occurred while processing your request."
        };
    }

    private static string GetErrorMessage(string? customMessage, string? responseMessage, ErrorResult? error)
    {
        if (!string.IsNullOrEmpty(customMessage))
        {
            return customMessage;
        }

        if (!string.IsNullOrEmpty(responseMessage))
        {
            return responseMessage;
        }

        if (!string.IsNullOrEmpty(error?.Message))
        {
            return error.Message;
        }

        if (error?.Messages.Count > 0)
        {
            return string.Join(", ", error.Messages);
        }

        return "An error occurred.";
    }
}

public class ValidationErrorResponse
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int Status { get; set; }
    public Dictionary<string, List<string>> Errors { get; set; } = new();
}
