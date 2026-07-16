// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Pondhawk.Api.Identity;

/// <summary>
/// Header-mode gateway auth: deserializes an unsigned JSON <see cref="ClaimSet"/> from the
/// <c>X-Gateway-Identity</c> header and trusts it (trust is the network boundary — only the gateway
/// can set the header). A missing header yields <see cref="AuthenticateResult.NoResult"/>.
/// </summary>
public sealed class GatewayHeaderAuthenticationHandler : AuthenticationHandler<GatewayAuthenticationSchemeOptions>
{
    /// <summary>Initializes a new instance.</summary>
    public GatewayHeaderAuthenticationHandler(
        IOptionsMonitor<GatewayAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(IdentityConstants.IdentityHeaderName, out var values) || values.Count == 0)
            return Task.FromResult(AuthenticateResult.NoResult());

        var json = values[0];
        if (string.IsNullOrWhiteSpace(json))
            return Task.FromResult(AuthenticateResult.NoResult());

        ClaimSet? claims;
        try
        {
            claims = JsonSerializer.Deserialize<ClaimSet>(json);
        }
        catch (JsonException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Malformed gateway identity header."));
        }

        if (claims is null)
            return Task.FromResult(AuthenticateResult.NoResult());

        var principal = ClaimSetPrincipal.ToPrincipal(claims, IdentityConstants.Scheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, IdentityConstants.Scheme)));
    }
}
