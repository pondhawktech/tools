using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Pondhawk.Api.Identity;

/// <summary>
/// HS256 (symmetric) implementation of <see cref="IGatewayTokenEncoder"/> using
/// <see cref="JsonWebTokenHandler"/> — no third-party JWT library required.
/// </summary>
public sealed class GatewayTokenJwtEncoder : IGatewayTokenEncoder
{
    private static readonly JsonWebTokenHandler Handler = new();

    private readonly SymmetricSecurityKey _key;

    /// <summary>Gets or sets the token lifetime applied on encode. Default 30 seconds.</summary>
    public TimeSpan TokenTimeToLive { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Initializes a new instance with the given HS256 signing key.</summary>
    /// <param name="signingKey">The symmetric signing key bytes.</param>
    public GatewayTokenJwtEncoder(byte[] signingKey)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        _key = new SymmetricSecurityKey(signingKey);
    }

    /// <inheritdoc />
    public string Encode(ClaimSet claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var descriptor = new SecurityTokenDescriptor
        {
            Claims = ToClaims(claims),
            Expires = DateTime.UtcNow.Add(TokenTimeToLive),
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256),
        };

        return Handler.CreateToken(descriptor);
    }

    /// <inheritdoc />
    public async Task<ClaimSet?> DecodeAsync(string token)
    {
        var result = await Handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            IssuerSigningKey = _key,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        }).ConfigureAwait(false);

        return result.IsValid ? FromClaims(result.ClaimsIdentity) : null;
    }

    private static Dictionary<string, object> ToClaims(ClaimSet cs)
    {
        var claims = new Dictionary<string, object>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(cs.UserId)) claims[IdentityConstants.SubjectClaim] = cs.UserId;
        if (!string.IsNullOrWhiteSpace(cs.UserName)) claims[IdentityConstants.UserNameClaim] = cs.UserName;
        if (!string.IsNullOrWhiteSpace(cs.FirstName)) claims[IdentityConstants.FirstNameClaim] = cs.FirstName;
        if (!string.IsNullOrWhiteSpace(cs.LastName)) claims[IdentityConstants.LastNameClaim] = cs.LastName;
        if (!string.IsNullOrWhiteSpace(cs.Email)) claims[IdentityConstants.EmailClaim] = cs.Email;
        if (cs.Roles.Count > 0) claims[IdentityConstants.RoleClaim] = cs.Roles.ToArray();

        return claims;
    }

    private static ClaimSet FromClaims(ClaimsIdentity identity) => new()
    {
        UserId = identity.FindFirst(IdentityConstants.SubjectClaim)?.Value ?? string.Empty,
        UserName = identity.FindFirst(IdentityConstants.UserNameClaim)?.Value ?? string.Empty,
        FirstName = identity.FindFirst(IdentityConstants.FirstNameClaim)?.Value ?? string.Empty,
        LastName = identity.FindFirst(IdentityConstants.LastNameClaim)?.Value ?? string.Empty,
        Email = identity.FindFirst(IdentityConstants.EmailClaim)?.Value ?? string.Empty,
        Roles = identity.FindAll(IdentityConstants.RoleClaim).Select(c => c.Value).ToList(),
    };
}
