using System.Net;
using Pondhawk.Exceptions;

namespace Pondhawk.Api.Exceptions;

/// <summary>
/// The canonical <see cref="ErrorKind"/> → HTTP status mapping for the web layer (the mapping
/// Pondhawk.Core deliberately leaves to ASP.NET). Shared by the response filter and the exception
/// handler so both render failures consistently.
/// </summary>
internal static class ErrorStatusMap
{
    public static int ToStatus(ErrorKind kind) => (int)(kind switch
    {
        ErrorKind.None => HttpStatusCode.OK,
        ErrorKind.NotFound => HttpStatusCode.NotFound,
        ErrorKind.NotImplemented => HttpStatusCode.NotImplemented,
        ErrorKind.Predicate => HttpStatusCode.UnprocessableEntity,
        ErrorKind.Conflict => HttpStatusCode.Conflict,
        ErrorKind.Concurrency => HttpStatusCode.Gone,
        ErrorKind.BadRequest => HttpStatusCode.BadRequest,
        ErrorKind.AuthenticationRequired => HttpStatusCode.Unauthorized,
        ErrorKind.NotAuthorized => HttpStatusCode.Forbidden,
        ErrorKind.Remote => HttpStatusCode.BadGateway,
        _ => HttpStatusCode.InternalServerError,
    });
}
