using System.Net;
using System.Text.Json;
using Backend.Application.Common;
using Backend.Common.Interfaces.Auth;
using Backend.Common.Models;
using Backend.Features.Auth;
using FluentValidation.Results;
using Microsoft.Data.SqlClient;

namespace Backend.Api.Middleware;

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
            logger.LogError(exception, exception.Message);
            var res = new Response { IsError = true };
            string email = currentUser.GetUserEmail() is string userEmail ? userEmail : "Anonymous";
            string userId = currentUser.GetUserId();
            string errorId = Guid.NewGuid().ToString();
            var errorResult = new ErrorResult { };
            if (exception is not KrafterException && exception.InnerException != null)
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
                case KrafterException e:
                    res.StatusCode = (int)e.StatusCode;
                    if (e.ErrorMessages is not null)
                    {
                        errorResult.Messages = e.ErrorMessages;
                    }

                    errorResult.Message = e.Message;
                    break;

                case SqlException sqlException:
                    res.StatusCode = (int)HttpStatusCode.InternalServerError;
                    res.Error.Message = "A database error occurred.";
                    switch (sqlException.Number)
                    {
                        case 2627: // Unique constraint error
                        case 547: // Constraint check violation
                        case 2601: // Duplicated key row error
                            // Constraint violation exception
                            res.Error.Message = "A constraint violation occurred in the database.";
                            break;
                        case 1205: // Deadlock
                            // Deadlock exception
                            res.Error.Message = "A deadlock occurred in the database.";
                            break;
                        // TODO: You can add more case statements here to handle other error codes
                        default:
                            // Unknown database error
                            res.Error.Message = "An unknown database error occurred.";
                            break;
                    }

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
