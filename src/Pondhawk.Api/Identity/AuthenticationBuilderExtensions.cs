using Microsoft.AspNetCore.Authentication;

namespace Pondhawk.Api.Identity;

/// <summary>Registers a gateway auth handler under the shared <see cref="IdentityConstants.Scheme"/>.</summary>
public static class AuthenticationBuilderExtensions
{
    /// <summary>Adds the token-mode (signed JWT) gateway handler.</summary>
    /// <param name="builder">The authentication builder.</param>
    /// <returns>The authentication builder for chaining.</returns>
    public static AuthenticationBuilder AddGatewayToken(this AuthenticationBuilder builder) =>
        builder.AddScheme<GatewayAuthenticationSchemeOptions, GatewayTokenAuthenticationHandler>(
            IdentityConstants.Scheme, _ => { });

    /// <summary>Adds the header-mode (unsigned JSON) gateway handler.</summary>
    /// <param name="builder">The authentication builder.</param>
    /// <returns>The authentication builder for chaining.</returns>
    public static AuthenticationBuilder AddGatewayHeader(this AuthenticationBuilder builder) =>
        builder.AddScheme<GatewayAuthenticationSchemeOptions, GatewayHeaderAuthenticationHandler>(
            IdentityConstants.Scheme, _ => { });

    /// <summary>
    /// Adds the development-mode handler that authenticates every request as <paramref name="identity"/>.
    /// FOR LOCAL DEVELOPMENT ONLY — never register this in a deployed configuration.
    /// </summary>
    /// <param name="builder">The authentication builder.</param>
    /// <param name="identity">The identity every request is authenticated as.</param>
    /// <returns>The authentication builder for chaining.</returns>
    public static AuthenticationBuilder AddGatewayDevelopment(this AuthenticationBuilder builder, ClaimSet identity) =>
        builder.AddScheme<GatewayDevelopmentAuthenticationSchemeOptions, GatewayDevelopmentAuthenticationHandler>(
            IdentityConstants.Scheme, options => options.Identity = identity);
}
