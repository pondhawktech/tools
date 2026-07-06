using System.Net.Http;

namespace Pondhawk.Api.Identity;

/// <summary>
/// A <see cref="DelegatingHandler"/> that attaches the gateway token from an
/// <see cref="IAccessTokenSource"/> to outbound requests (as the gateway token header), so a call to
/// another Pondhawk service carries the caller's identity.
/// </summary>
/// <param name="source">The token source.</param>
public sealed class GatewayTokenHttpHandler(IAccessTokenSource source) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = await source.GetTokenAsync().ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Remove(IdentityConstants.TokenHeaderName);
            request.Headers.TryAddWithoutValidation(IdentityConstants.TokenHeaderName, token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
