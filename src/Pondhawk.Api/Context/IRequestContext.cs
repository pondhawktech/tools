using System.Security.Claims;

namespace Pondhawk.Api.Context;

/// <summary>
/// Per-request ambient context: the correlation id, the authenticated caller, and the inbound
/// gateway token. Populated by the diagnostics-enrichment middleware and consumed by the response
/// filter, exception handler, logging behavior, and outbound token propagation.
/// </summary>
public interface IRequestContext
{
    /// <summary>Gets the correlation id for the current request, if one has been established.</summary>
    string? CorrelationId { get; }

    /// <summary>Gets or sets the authenticated caller principal for the current request.</summary>
    ClaimsPrincipal? Caller { get; set; }

    /// <summary>Gets or sets the raw inbound gateway token, for re-propagation on outbound calls.</summary>
    string? CallerGatewayToken { get; set; }
}
