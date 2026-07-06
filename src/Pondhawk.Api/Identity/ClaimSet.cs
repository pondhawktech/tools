using System.Text.Json.Serialization;

namespace Pondhawk.Api.Identity;

/// <summary>
/// The minimal identity payload carried by the gateway (as JSON in header mode, or as JWT claims in
/// token mode). Deliberately lean — user identity, name, and roles.
/// </summary>
public sealed class ClaimSet
{
    /// <summary>Gets or sets the user identity (subject).</summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the user name.</summary>
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the user's first (given) name.</summary>
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Gets or sets the user's last (family) name.</summary>
    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>Gets or sets the user's email.</summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets the user's roles.</summary>
    [JsonPropertyName("roles")]
    public IList<string> Roles { get; set; } = [];
}
