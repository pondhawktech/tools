// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Pondhawk.Api.Context;
using Pondhawk.Exceptions;
using Serilog;

namespace Pondhawk.Api.Exceptions;

/// <summary>
/// Global <see cref="IExceptionHandler"/> that renders an unhandled exception as a
/// <see cref="ProblemDetail"/> (<c>application/problem+json</c>). An <see cref="ExternalException"/>
/// keeps its <see cref="ErrorKind"/>; a <see cref="JsonException"/> becomes <see cref="ErrorKind.BadRequest"/>;
/// anything else is <see cref="ErrorKind.System"/> (500) and is logged with context.
/// </summary>
/// <remarks>
/// ASP.NET resolves <see cref="IExceptionHandler"/> as a singleton (via <c>AddExceptionHandler&lt;T&gt;</c>),
/// so the per-request <see cref="IRequestContext"/> must not be constructor-injected — it is resolved from
/// <see cref="HttpContext.RequestServices"/> at handle-time instead.
/// </remarks>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private static readonly ILogger Logger = Log.ForContext<ApiExceptionHandler>();

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (kind, errorCode, explanation, details) = Describe(exception);
        var status = ErrorStatusMap.ToStatus(kind);

        if (status >= 500)
            Logger.Error(exception, "Unhandled {Kind} dispatching {Path}", kind, httpContext.Request.Path.Value);
        else
            Logger.Debug(exception, "Handled {Kind} for {Path}", kind, httpContext.Request.Path.Value);

        var correlationId = httpContext.RequestServices.GetService<IRequestContext>()?.CorrelationId ?? string.Empty;

        var problem = new ProblemDetail
        {
            Type = kind.ToString(),
            Title = errorCode,
            Detail = explanation,
            StatusCode = status,
            Instance = httpContext.Request.Path.Value ?? string.Empty,
            CorrelationId = correlationId,
            Segments = [.. details],
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response
            .WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private static (ErrorKind Kind, string ErrorCode, string Explanation, IEnumerable<EventDetail> Details) Describe(Exception exception) =>
        exception switch
        {
            ExternalException ex => (ex.Kind, ex.ErrorCode, ex.Explanation, ex.Details),
            JsonException ex => (ErrorKind.BadRequest, "BadJsonRequest", ex.Message, []),
            _ => (ErrorKind.System, "System", exception.Message, []),
        };
}
