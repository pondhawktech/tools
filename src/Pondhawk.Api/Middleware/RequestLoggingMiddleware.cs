// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Pondhawk.Api.Identity;
using Pondhawk.Logging;
using Serilog;
using Serilog.Events;

namespace Pondhawk.Api.Middleware;

/// <summary>
/// When debug logging is enabled for the <c>Pondhawk.Diagnostics.Http</c> category, logs the full
/// inbound request — method, path, query, redacted headers, and a buffered (rewound) body — as a Watch
/// text payload, then the response status. A no-op when the category is not debug-enabled.
/// </summary>
/// <remarks>
/// Register this <em>deep</em> in the pipeline (after routing/auth) so it can capture the resolved
/// endpoint and authenticated user — accepting that anything short-circuited earlier won't reach it.
/// The early, whole-pipeline bracket + total timing is <see cref="DiagnosticsMonitorMiddleware"/>'s
/// job; the two are complementary by position, not redundant.
/// </remarks>
/// <param name="next">The next middleware.</param>
public sealed class RequestLoggingMiddleware(RequestDelegate next)
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "Pondhawk.Diagnostics.Http");

    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The HTTP context.</param>
    public async Task Invoke(HttpContext context)
    {
        if (!Logger.IsEnabled(LogEventLevel.Debug))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        context.Request.EnableBuffering();
        var body = await ReadBodyAsync(context.Request).ConfigureAwait(false);
        var summary = BuildSummary(context.Request, body);

        Logger
            .ForContext(LogPropertyNames.PayloadType, (int)PayloadType.Text)
            .ForContext(LogPropertyNames.PayloadContent, summary)
            .Debug("HTTP {Method} {Path}", context.Request.Method, context.Request.Path.Value);

        await next(context).ConfigureAwait(false);

        Logger.Debug("HTTP {Method} {Path} -> {Status}",
            context.Request.Method, context.Request.Path.Value, context.Response.StatusCode);
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0)
            return string.Empty;

        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync().ConfigureAwait(false);
        request.Body.Position = 0;
        return text;
    }

    private static string BuildSummary(HttpRequest request, string body)
    {
        var builder = new StringBuilder();
        builder.Append(request.Method).Append(' ').Append(request.Path).Append(request.QueryString).Append('\n');

        foreach (var header in request.Headers)
            builder.Append(header.Key).Append(": ").Append(Redact(header.Key, header.Value.ToString())).Append('\n');

        if (!string.IsNullOrEmpty(body))
            builder.Append('\n').Append(body);

        return builder.ToString();
    }

    private static string Redact(string name, string value)
    {
        var length = value.Length.ToString(CultureInfo.InvariantCulture);

        if (string.Equals(name, "Authorization", StringComparison.OrdinalIgnoreCase))
        {
            var space = value.IndexOf(' ', StringComparison.Ordinal);
            var scheme = space > 0 ? value[..space] : "token";
            return $"{scheme} ***({length})";
        }

        if (string.Equals(name, IdentityConstants.TokenHeaderName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, IdentityConstants.IdentityHeaderName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Cookie", StringComparison.OrdinalIgnoreCase))
        {
            return $"***({length})";
        }

        return value;
    }
}
