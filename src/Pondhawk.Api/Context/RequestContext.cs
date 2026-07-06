using System.Globalization;
using System.Security.Claims;
using Pondhawk.Logging;

namespace Pondhawk.Api.Context;

/// <summary>
/// Default scoped <see cref="IRequestContext"/>. It owns the correlation id: an explicitly assigned
/// value wins; otherwise it adopts the ambient <see cref="CorrelationManager.Current"/>; otherwise it
/// generates a fresh id and caches it — so <see cref="CorrelationId"/> is always non-null and stable
/// for the request, even when no <see cref="System.Diagnostics.Activity"/> is present.
/// </summary>
internal sealed class RequestContext : IRequestContext
{
    private string? _correlationId;

    public string? CorrelationId
    {
        get => _correlationId ??= CorrelationManager.Current ?? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        set => _correlationId = value;
    }

    public ClaimsPrincipal? Caller { get; set; }

    public string? CallerGatewayToken { get; set; }
}
