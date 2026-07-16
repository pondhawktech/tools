// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Pondhawk.Api.Filters;

/// <summary>An <see cref="AbstractApiKeyValidator"/> over a fixed key string.</summary>
/// <param name="apiKey">The expected API key.</param>
public sealed class SimpleApiKeyValidator(string apiKey) : AbstractApiKeyValidator
{
    /// <inheritdoc />
    protected override string GetApiKey() => apiKey;
}
