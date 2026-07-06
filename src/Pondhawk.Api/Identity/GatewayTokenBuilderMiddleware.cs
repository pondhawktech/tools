using Microsoft.AspNetCore.Http;

namespace Pondhawk.Api.Identity;

/// <summary>
/// Mints a signed gateway token from the current authenticated user and writes it to the request's
/// <c>X-Gateway-Identity-Token</c> header, so downstream handlers/proxies see the identity in token
/// mode. Unauthenticated requests pass through untouched.
/// </summary>
/// <param name="next">The next middleware.</param>
public sealed class GatewayTokenBuilderMiddleware(RequestDelegate next)
{
    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="encoder">The token encoder.</param>
    public async Task Invoke(HttpContext context, IGatewayTokenEncoder encoder)
    {
        context.Request.Headers.Remove(IdentityConstants.TokenHeaderName);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claims = ClaimSetPrincipal.FromPrincipal(context.User);
            context.Request.Headers[IdentityConstants.TokenHeaderName] = encoder.Encode(claims);
        }

        await next(context).ConfigureAwait(false);
    }
}
