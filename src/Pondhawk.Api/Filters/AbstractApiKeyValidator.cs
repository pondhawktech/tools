// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;

namespace Pondhawk.Api.Filters;

/// <summary>Base validator that compares against a configured key using a fixed-time comparison.</summary>
public abstract class AbstractApiKeyValidator : IApiKeyValidator
{
    /// <summary>Gets the expected API key.</summary>
    protected abstract string GetApiKey();

    /// <inheritdoc />
    public bool IsValid(string candidate)
    {
        if (string.IsNullOrEmpty(candidate))
            return false;

        var presented = Encoding.ASCII.GetBytes(candidate);
        var expected = Encoding.ASCII.GetBytes(GetApiKey());

        return CryptographicOperations.FixedTimeEquals(presented, expected);
    }
}
