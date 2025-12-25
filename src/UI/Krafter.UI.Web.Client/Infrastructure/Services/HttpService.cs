using System.Net.Http.Json;
using System.Text.Json;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Features.Auth._Shared;


namespace Krafter.UI.Web.Client.Infrastructure.Services;

public class HttpService
{
    private readonly HttpClient _httpClient;
    private readonly NotificationService _notificationService;

    public HttpService(IHttpClientFactory httpClientFactory, NotificationService notificationService)
    {
        _httpClient = httpClientFactory.CreateClient("KrafterUIAPI"); // Or "KrafterUIAPI"
        _notificationService = notificationService;
    }

    public async Task<Response<T>> GetAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<Response<T>>();
        }

        return await HandleErrorResponse<T>(response);
    }

    public async Task<Response<T>> PostAsync<T>(string url, object data, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(url, data, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<Response<T>>();
        }

        return await HandleErrorResponse<T>(response);
    }

    public async Task<Response> PostAsync(string url, object data, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(url, data, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<Response>();
        }

        return await HandleErrorResponse(response);
    }

    public async Task<Response<T>> PutAsync<T>(string url, object data, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(url, data, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<Response<T>>();
        }

        return await HandleErrorResponse<T>(response);
    }

    public async Task<Response> PutAsync(string url, object data, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await _httpClient.PutAsJsonAsync(url, data, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<Response>();
        }

        return await HandleErrorResponse(response);
    }

    private async Task<Response> HandleErrorResponse(HttpResponseMessage response)
    {
        string errorContent = await response.Content.ReadAsStringAsync();
        Response? errorResponse = JsonSerializer.Deserialize<Response>(errorContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        ValidationErrorResponse? validationErrorResponse = JsonSerializer.Deserialize<ValidationErrorResponse>(
            errorContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (errorResponse != null && errorResponse.StatusCode != 0 && errorResponse.StatusCode != 200)
        {
            errorResponse.IsError = true;
            string errorMessage = string.Join("\n", errorResponse.Error.Messages);
            errorMessage += $"\nErrorCode: {errorResponse.Error.ErrorCode}";
            errorMessage += $"\nSupportMessage: {errorResponse.Error.Message}";
            _notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error, Summary = $"Error: {errorMessage}"
            });
            return errorResponse;
        }

        if (validationErrorResponse != null)
        {
            string errorMessage = $"Validation Error: {validationErrorResponse.Title}";
            foreach (KeyValuePair<string, List<string>> error in validationErrorResponse.Errors)
            {
                errorMessage += $"\n{error.Key}: {string.Join(", ", error.Value)}";
            }

            _notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error, Summary = errorMessage
            });

            return new Response { StatusCode = (int)response.StatusCode, IsError = true };
        }

        return new Response { IsError = true };
    }

    private async Task<Response<T>> HandleErrorResponse<T>(HttpResponseMessage response)
    {
        string errorContent = await response.Content.ReadAsStringAsync();
        Response<T>? errorResponse = JsonSerializer.Deserialize<Response<T>>(errorContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        ValidationErrorResponse? validationErrorResponse = JsonSerializer.Deserialize<ValidationErrorResponse>(
            errorContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (errorResponse != null && errorResponse.StatusCode != 0 && errorResponse.StatusCode != 200)
        {
            errorResponse.IsError = true;
            string errorMessage = string.Join("\n", errorResponse.Error.Messages);
            errorMessage += $"\nErrorCode: {errorResponse.Error.ErrorCode}";
            errorMessage += $"\nSupportMessage: {errorResponse.Error.Message}";
            _notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error, Summary = $"Error: {errorMessage}"
            });
            return errorResponse;
        }

        if (validationErrorResponse != null)
        {
            string errorMessage = $"Validation Error: {validationErrorResponse.Title}";
            foreach (KeyValuePair<string, List<string>> error in validationErrorResponse.Errors)
            {
                errorMessage += $"\n{error.Key}: {string.Join(", ", error.Value)}";
            }

            _notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error, Summary = errorMessage
            });

            return new Response<T> { StatusCode = (int)response.StatusCode, IsError = true };
        }

        return new Response<T> { IsError = true };
    }
}
