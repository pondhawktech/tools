// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Pondhawk.Api.Identity;

/// <summary>
/// Development-mode gateway auth: authenticates every request as a fixed, configured
/// <see cref="ClaimSet"/> — with no gateway and no token. This exists so an app's authenticated code
/// paths (a "current user" endpoint, role-gated routes) can be exercised locally without standing up
/// the gateway. FOR LOCAL DEVELOPMENT ONLY — it authenticates unconditionally, so it must never be
/// registered in a deployed configuration.
/// </summary>
public sealed class GatewayDevelopmentAuthenticationHandler : AuthenticationHandler<GatewayDevelopmentAuthenticationSchemeOptions>
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="options">The scheme options carrying the configured development identity.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    public GatewayDevelopmentAuthenticationHandler(
        IOptionsMonitor<GatewayDevelopmentAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var principal = ClaimSetPrincipal.ToPrincipal(Options.Identity, IdentityConstants.Scheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, IdentityConstants.Scheme)));
    }
}
