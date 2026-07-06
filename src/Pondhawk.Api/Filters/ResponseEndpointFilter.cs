using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Pondhawk.Api.Context;
using Pondhawk.Api.Exceptions;
using Pondhawk.Exceptions;
using Pondhawk.Mediator;

namespace Pondhawk.Api.Filters;

/// <summary>
/// Endpoint filter that renders a handler's <see cref="Response{T}"/> (via <see cref="IResponse"/>)
/// to an HTTP result: success becomes <c>Ok</c>/JSON/stream, failure becomes a
/// <see cref="ProblemDetail"/> (<c>application/problem+json</c>) whose status is mapped from the
/// <see cref="ErrorKind"/>. Handlers therefore return the envelope and stay transport-agnostic.
/// </summary>
public class ResponseEndpointFilter(IRequestContext requestContext, JsonSerializerOptions options) : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context).ConfigureAwait(false);

        if (result is not IResponse response)
            return result;

        if (response.Ok)
        {
            if (response.Value is Stream stream)
                return Results.Stream(stream, "application/json");

            return response.Value is null ? Results.Ok() : Results.Json(response.Value, options);
        }

        var error = response.Error!;
        var status = MapToStatus(error.Kind);

        var problem = new ProblemDetail
        {
            Type = error.Kind.ToString(),
            Title = error.ErrorCode,
            Detail = error.Explanation,
            StatusCode = status,
            Instance = context.HttpContext.Request.Path.Value ?? string.Empty,
            CorrelationId = requestContext.CorrelationId ?? string.Empty,
            Segments = [.. error.Details],
        };

        return Results.Json(problem, options, "application/problem+json", status);
    }

    /// <summary>Maps an <see cref="ErrorKind"/> to an HTTP status code. Override to customize.</summary>
    /// <param name="kind">The error kind.</param>
    /// <returns>The HTTP status code.</returns>
    protected virtual int MapToStatus(ErrorKind kind) => ErrorStatusMap.ToStatus(kind);
}
