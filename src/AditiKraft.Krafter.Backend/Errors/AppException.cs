using System.Net;

namespace AditiKraft.Krafter.Backend.Errors;

public class AppException(
    string message,
    List<string>? errors = default,
    HttpStatusCode statusCode = HttpStatusCode.BadRequest)
    : Exception(message)
{
    public List<string>? ErrorMessages { get; } = errors;

    public HttpStatusCode StatusCode { get; } = statusCode;
}

public class ForbiddenException(string message) : AppException(message, null, HttpStatusCode.Forbidden);

public class UnauthorizedException(string message) : AppException(message, null, HttpStatusCode.Unauthorized);

public class NotFoundException(string message) : AppException(message, null, HttpStatusCode.NotFound);

public class ConflictException(string message) : AppException(message, null, HttpStatusCode.Conflict);

