namespace Pondhawk.Api.Identity;

/// <summary>Supplies a gateway token for outbound (service-to-service) calls.</summary>
public interface IAccessTokenSource
{
    /// <summary>Gets a name identifying this token source.</summary>
    string Name { get; }

    /// <summary>Gets a value indicating whether the current token has expired.</summary>
    bool HasExpired { get; }

    /// <summary>Gets the current token to attach to an outbound request.</summary>
    /// <returns>The token string (possibly empty).</returns>
    Task<string> GetTokenAsync();
}
