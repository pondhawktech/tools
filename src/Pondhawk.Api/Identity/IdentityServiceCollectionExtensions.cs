using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Pondhawk.Api.Identity;

/// <summary>Registration for gateway authentication.</summary>
public static class IdentityServiceCollectionExtensions
{
    /// <summary>
    /// Registers token-mode gateway auth: an HS256 <see cref="IGatewayTokenEncoder"/> from the
    /// base64 signing key, plus the token authentication handler under
    /// <see cref="IdentityConstants.Scheme"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="signingKeyBase64">The base64-encoded HS256 signing key.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGatewayTokenAuthentication(this IServiceCollection services, string signingKeyBase64)
    {
        var key = Convert.FromBase64String(signingKeyBase64);
        services.AddSingleton<IGatewayTokenEncoder>(new GatewayTokenJwtEncoder(key));
        services.AddAuthentication(IdentityConstants.Scheme).AddGatewayToken();

        return services;
    }

    /// <summary>
    /// Registers header-mode gateway auth (unsigned JSON identity header) under
    /// <see cref="IdentityConstants.Scheme"/>. No signing key required.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGatewayHeaderAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(IdentityConstants.Scheme).AddGatewayHeader();

        return services;
    }
}
