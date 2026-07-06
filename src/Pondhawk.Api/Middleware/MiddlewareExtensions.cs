using Microsoft.AspNetCore.Builder;

namespace Pondhawk.Api.Middleware;

/// <summary>Registers the Pondhawk.Api diagnostics/logging middlewares.</summary>
public static class MiddlewareExtensions
{
    /// <summary>Populates the request context with correlation, caller, and gateway token. Run after authentication.</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseDiagnosticsEnrichment(this IApplicationBuilder app) =>
        app.UseMiddleware<DiagnosticsEnrichmentMiddleware>();

    /// <summary>Brackets each request with begin/end + elapsed timing diagnostics.</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseDiagnosticsMonitor(this IApplicationBuilder app) =>
        app.UseMiddleware<DiagnosticsMonitorMiddleware>();

    /// <summary>Logs full inbound requests when the diagnostics category is debug-enabled.</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestLoggingMiddleware>();
}
