using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Pondhawk.Api.Identity;

/// <summary>
/// Serializes the current authenticated user to an unsigned JSON claim set and writes it to the
/// request's <c>X-Gateway-Identity</c> header (header mode). Unauthenticated requests pass through.
/// </summary>
/// <param name="next">The next middleware.</param>
public sealed class GatewayHeaderBuilderMiddleware(RequestDelegate next)
{
    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The HTTP context.</param>
    public async Task Invoke(HttpContext context)
    {
        context.Request.Headers.Remove(IdentityConstants.IdentityHeaderName);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claims = ClaimSetPrincipal.FromPrincipal(context.User);
            context.Request.Headers[IdentityConstants.IdentityHeaderName] = JsonSerializer.Serialize(claims);
        }

        await next(context).ConfigureAwait(false);
    }
}
