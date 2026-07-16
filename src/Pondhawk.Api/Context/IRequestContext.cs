// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Claims;

namespace Pondhawk.Api.Context;

/// <summary>
/// Per-request ambient context: the correlation id, the authenticated caller, and the inbound
/// gateway token. Populated by the diagnostics-enrichment middleware and consumed by the response
/// filter, exception handler, logging behavior, and outbound token propagation.
/// </summary>
public interface IRequestContext
{
    /// <summary>
    /// Gets or sets the correlation id for the current request. The default implementation owns a
    /// stable id (generating one if none was supplied), so this is reliably non-null regardless of
    /// whether an ambient <see cref="System.Diagnostics.Activity"/> exists.
    /// </summary>
    string? CorrelationId { get; set; }

    /// <summary>Gets or sets the authenticated caller principal for the current request.</summary>
    ClaimsPrincipal? Caller { get; set; }

    /// <summary>Gets or sets the raw inbound gateway token, for re-propagation on outbound calls.</summary>
    string? CallerGatewayToken { get; set; }
}
