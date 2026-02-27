using System.Net;

namespace AditiKraft.Krafter.Backend.Errors;

public class KrafterException(
    string message,
    List<string>? errors = default,
    HttpStatusCode statusCode = HttpStatusCode.BadRequest)
    : Exception(message)
{
    public List<string>? ErrorMessages { get; } = errors;

    public HttpStatusCode StatusCode { get; } = statusCode;
}

public class ForbiddenException(string message) : KrafterException(message, null, HttpStatusCode.Forbidden);

public class UnauthorizedException(string message) : KrafterException(message, null, HttpStatusCode.Unauthorized);

public class NotFoundException(string message) : KrafterException(message, null, HttpStatusCode.NotFound);

public class ConflictException(string message) : KrafterException(message, null, HttpStatusCode.Conflict);

