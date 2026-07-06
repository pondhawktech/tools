namespace Pondhawk.Api.Filters;

/// <summary>An <see cref="AbstractApiKeyValidator"/> over a fixed key string.</summary>
/// <param name="apiKey">The expected API key.</param>
public sealed class SimpleApiKeyValidator(string apiKey) : AbstractApiKeyValidator
{
    /// <inheritdoc />
    protected override string GetApiKey() => apiKey;
}
