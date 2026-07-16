// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Pondhawk.Api.Identity;

/// <summary>
/// Token-mode gateway auth: validates the HS256 JWT in the <c>X-Gateway-Identity-Token</c> header and
/// builds a <see cref="System.Security.Claims.ClaimsPrincipal"/> from it. A missing header yields
/// <see cref="AuthenticateResult.NoResult"/> (anonymous); an invalid token yields a failure.
/// </summary>
public sealed class GatewayTokenAuthenticationHandler : AuthenticationHandler<GatewayAuthenticationSchemeOptions>
{
    private readonly IGatewayTokenEncoder _tokenEncoder;

    /// <summary>Initializes a new instance.</summary>
    public GatewayTokenAuthenticationHandler(
        IGatewayTokenEncoder tokenEncoder,
        IOptionsMonitor<GatewayAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _tokenEncoder = tokenEncoder;
    }

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(IdentityConstants.TokenHeaderName, out var values) || values.Count == 0)
            return AuthenticateResult.NoResult();

        var token = values[0];
        if (string.IsNullOrWhiteSpace(token))
            return AuthenticateResult.NoResult();

        var claims = await _tokenEncoder.DecodeAsync(token).ConfigureAwait(false);
        if (claims is null)
            return AuthenticateResult.Fail("Invalid or expired gateway token.");

        var principal = ClaimSetPrincipal.ToPrincipal(claims, IdentityConstants.Scheme);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, IdentityConstants.Scheme));
    }
}
