using System.Net;

namespace Krafter.Shared.Common.Models;

public class Response<T>
{
    public bool IsError { get; set; } = false;
    public int StatusCode { get; set; } = (int)HttpStatusCode.OK;
    public T? Data { get; set; }
    public string? Message { get; set; }
    public ErrorResult Error { get; set; } = new();

    // Factory methods for error responses
    public static Response<T> NotFound(string message) => CreateError(message, HttpStatusCode.NotFound);
    public static Response<T> Conflict(string message) => CreateError(message, HttpStatusCode.Conflict);
    public static Response<T> Forbidden(string message) => CreateError(message, HttpStatusCode.Forbidden);
    public static Response<T> Unauthorized(string message) => CreateError(message, HttpStatusCode.Unauthorized);
    public static Response<T> BadRequest(string message) => CreateError(message, HttpStatusCode.BadRequest);

    public static Response<T> CustomError(string message, int statusCode) =>
        CreateError(message, (HttpStatusCode)statusCode);

    // Factory method for success response
    public static Response<T> Success(T data, string? message = null) => new()
    {
        IsError = false, StatusCode = (int)HttpStatusCode.OK, Data = data, Message = message
    };

    private static Response<T> CreateError(string message, HttpStatusCode statusCode) => new()
    {
        IsError = true,
        StatusCode = (int)statusCode,
        Message = message,
        Error = new ErrorResult { Message = message },
        Data = default
    };
}

public class Response
{
    public bool IsError { get; set; } = false;
    public int StatusCode { get; set; } = (int)HttpStatusCode.OK;
    public string? Message { get; set; }
    public ErrorResult Error { get; set; } = new();

    // Factory methods for error responses
    public static Response NotFound(string message) => CreateError(message, HttpStatusCode.NotFound);
    public static Response Conflict(string message) => CreateError(message, HttpStatusCode.Conflict);
    public static Response Forbidden(string message) => CreateError(message, HttpStatusCode.Forbidden);
    public static Response Unauthorized(string message) => CreateError(message, HttpStatusCode.Unauthorized);
    public static Response BadRequest(string message) => CreateError(message, HttpStatusCode.BadRequest);

    public static Response CustomError(string message, int statusCode) =>
        CreateError(message, (HttpStatusCode)statusCode);

    // Factory method for success response
    public static Response Success(string? message = null) => new()
    {
        IsError = false, StatusCode = (int)HttpStatusCode.OK, Message = message
    };

    private static Response CreateError(string message, HttpStatusCode statusCode) => new()
    {
        IsError = true,
        StatusCode = (int)statusCode,
        Message = message,
        Error = new ErrorResult { Message = message }
    };
}

public class ErrorResult
{
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
    public List<string> Messages { get; set; } = new();
}
