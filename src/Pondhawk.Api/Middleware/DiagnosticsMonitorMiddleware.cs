using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Pondhawk.Api.Context;
using Serilog;

namespace Pondhawk.Api.Middleware;

/// <summary>
/// Brackets each request with begin/end diagnostics and elapsed timing, logged to the
/// <c>Pondhawk.Diagnostics.Http</c> category via Pondhawk.Logging.
/// </summary>
/// <param name="next">The next middleware.</param>
public sealed class DiagnosticsMonitorMiddleware(RequestDelegate next)
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "Pondhawk.Diagnostics.Http");

    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="requestContext">The per-request context.</param>
    public async Task Invoke(HttpContext context, IRequestContext requestContext)
    {
        var started = Stopwatch.GetTimestamp();

        Logger.Debug("Begin {Method} {Path} (correlation {CorrelationId})",
            context.Request.Method, context.Request.Path.Value, requestContext.CorrelationId);

        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture);

            Logger.Debug("End {Method} {Path} -> {Status} ({Elapsed}ms)",
                context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, elapsedMs);
        }
    }
}
