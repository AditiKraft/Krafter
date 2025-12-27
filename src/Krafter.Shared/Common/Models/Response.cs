using System.Net;

namespace Krafter.Shared.Common.Models;

public class Response<T>
{
    public bool IsError { get; set; } = false;
    public int StatusCode { get; set; } = (int)HttpStatusCode.OK;
    public T? Data { get; set; }
    public string? Message { get; set; }
    public ErrorResult Error { get; set; } = new();
}

public class Response
{
    public bool IsError { get; set; } = false;
    public int StatusCode { get; set; } = (int)HttpStatusCode.OK;
    public string? Message { get; set; }
    public ErrorResult Error { get; set; } = new();
}

public class ErrorResult
{
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
    public List<string> Messages { get; set; } = new();
}
