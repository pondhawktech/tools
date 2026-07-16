// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;

namespace Pondhawk.Api.Identity;

/// <summary>
/// Options for the development-mode gateway auth handler: the fixed identity every request is
/// authenticated as. FOR LOCAL DEVELOPMENT ONLY — the handler authenticates unconditionally and
/// bypasses the gateway, so it must never be wired in a deployed configuration.
/// </summary>
public sealed class GatewayDevelopmentAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    /// <summary>Gets or sets the identity every request is authenticated as.</summary>
    public ClaimSet Identity { get; set; } = new();
}
