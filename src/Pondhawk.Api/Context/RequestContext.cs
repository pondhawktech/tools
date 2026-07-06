using System.Security.Claims;
using Pondhawk.Logging;

namespace Pondhawk.Api.Context;

/// <summary>
/// Default scoped <see cref="IRequestContext"/>. Correlation id comes from
/// <see cref="CorrelationManager.Current"/> (the ambient <see cref="System.Diagnostics.Activity"/>
/// baggage established per request).
/// </summary>
internal sealed class RequestContext : IRequestContext
{
    public string? CorrelationId => CorrelationManager.Current;

    public ClaimsPrincipal? Caller { get; set; }

    public string? CallerGatewayToken { get; set; }
}
