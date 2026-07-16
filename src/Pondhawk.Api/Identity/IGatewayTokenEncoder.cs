// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Pondhawk.Api.Identity;

/// <summary>Encodes/decodes a <see cref="ClaimSet"/> as a signed gateway token (JWT).</summary>
public interface IGatewayTokenEncoder
{
    /// <summary>Encodes the claim set as a signed token.</summary>
    /// <param name="claims">The claim set.</param>
    /// <returns>The signed token string.</returns>
    string Encode(ClaimSet claims);

    /// <summary>Validates a token and returns its claim set, or <see langword="null"/> if invalid/expired.</summary>
    /// <param name="token">The token string.</param>
    /// <returns>The decoded claim set, or <see langword="null"/>.</returns>
    Task<ClaimSet?> DecodeAsync(string token);
}
