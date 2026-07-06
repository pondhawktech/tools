using System.Text.Json;

namespace Pondhawk.Api.Json;

/// <summary>
/// A <see cref="JsonNamingPolicy"/> that preserves property names as declared (PascalCase),
/// i.e. a no-op policy for APIs that serialize in PascalCase.
/// </summary>
public sealed class PascalJsonNamingPolicy : JsonNamingPolicy
{
    /// <inheritdoc />
    public override string ConvertName(string name) => name;
}
