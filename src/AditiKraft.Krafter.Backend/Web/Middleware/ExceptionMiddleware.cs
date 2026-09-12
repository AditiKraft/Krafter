using System.Net;
using System.Text.Json;
using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Errors;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Models;
using FluentValidation.Results;
using Npgsql;

namespace AditiKraft.Krafter.Backend.Web.Middleware;

public class ExceptionMiddleware(ICurrentUser currentUser, ILogger<ExceptionMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            // For non-API requests (Blazor pages, static files), re-throw so the standard
            // ASP.NET Core error handling (UseExceptionHandler("/Error")) shows a proper error page
            // instead of raw JSON.
            if (!context.Request.Path.StartsWithSegments($"/{ApiRoutes.ApiPrefix}"))
            {
                throw;
            }

            logger.LogError(exception, exception.Message);
            var res = new Response { IsError = true };
            string email = currentUser.GetUserEmail() is string userEmail ? userEmail : "Anonymous";
            string userId = currentUser.GetUserId();
            string errorId = Guid.NewGuid().ToString();
            var errorResult = new ErrorResult { };
            if (exception is not AppException && exception.InnerException != null)
            {
                while (exception.InnerException != null)
                {
                    exception = exception.InnerException;
                }
            }

            if (exception is FluentValidation.ValidationException fluentException)
            {
                errorResult.Message = "One or More Validations failed.";
                foreach (ValidationFailure? error in fluentException.Errors)
                {
                    errorResult.Messages.Add(error.ErrorMessage);
                }
            }

            switch (exception)
            {
                case AppException e:
                    res.StatusCode = (int)e.StatusCode;
                    if (e.ErrorMessages is not null)
                    {
                        errorResult.Messages = e.ErrorMessages;
                    }

                    errorResult.Message = e.Message;
                    break;

                case NpgsqlException:
                    res.StatusCode = (int)HttpStatusCode.InternalServerError;
                    errorResult.Message = "A database error occurred.";
                    break;

                case KeyNotFoundException:
                    res.StatusCode = (int)HttpStatusCode.NotFound;
                    break;

                case FluentValidation.ValidationException:
                    res.StatusCode = (int)HttpStatusCode.BadRequest;
                    break;

                default:
                    res.StatusCode = (int)HttpStatusCode.InternalServerError;
                    break;
            }

            res.Error = errorResult;
            HttpResponse response = context.Response;
            if (!response.HasStarted)
            {
                response.ContentType = "application/json";
                response.StatusCode = res.StatusCode;
                await response.WriteAsync(JsonSerializer.Serialize(res));
            }
            else
            {
                // Log.Warning("Can't write error response. Response has already started.");
            }
        }
    }
}
