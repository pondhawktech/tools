using Microsoft.AspNetCore.Http;
using Pondhawk.Api.Context;
using Pondhawk.Api.Identity;
using Pondhawk.Logging;

namespace Pondhawk.Api.Middleware;

/// <summary>
/// Establishes the request's correlation id (from an <c>X-Correlation-Id</c> header or a fresh one)
/// and populates <see cref="IRequestContext"/> with the authenticated caller and inbound gateway
/// token. Run this after authentication so <c>context.User</c> is available.
/// </summary>
/// <param name="next">The next middleware.</param>
public sealed class DiagnosticsEnrichmentMiddleware(RequestDelegate next)
{
    /// <summary>The header carrying an incoming correlation id.</summary>
    public const string CorrelationHeader = "X-Correlation-Id";

    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="requestContext">The per-request context to populate.</param>
    public async Task Invoke(HttpContext context, IRequestContext requestContext)
    {
        var incoming = context.Request.Headers.TryGetValue(CorrelationHeader, out var values) && values.Count > 0
            ? values[0]
            : null;

        if (!string.IsNullOrWhiteSpace(incoming))
            requestContext.CorrelationId = incoming;

        // Flow the resolved id (reading CorrelationId generates one if needed) to the ambient
        // logging correlation, so log events carry it when an Activity is present.
        CorrelationManager.Set(requestContext.CorrelationId);

        requestContext.Caller = context.User;

        if (context.Request.Headers.TryGetValue(IdentityConstants.TokenHeaderName, out var token) && token.Count > 0)
            requestContext.CallerGatewayToken = token[0];

        await next(context).ConfigureAwait(false);
    }
}
