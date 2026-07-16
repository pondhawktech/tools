// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Pondhawk.Api.Context;
using Pondhawk.Logging;
using Pondhawk.Logging.Utilities;
using Pondhawk.Mediator;
using Serilog;

namespace Pondhawk.Api.Behaviors;

/// <summary>
/// Mediator pipeline behavior that logs each request's dispatch (with correlation context) via
/// Pondhawk.Logging, wrapped in a method-tracing scope.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse>(IRequestContext requestContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ILogger Logger = Log.ForContext<LoggingBehavior<TRequest, TResponse>>();

    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        using var _ = Logger.EnterMethod();

        Logger.Debug("Dispatching {Request} (correlation {CorrelationId})",
            typeof(TRequest).GetConciseName(), requestContext.CorrelationId);

        return await next().ConfigureAwait(false);
    }
}
