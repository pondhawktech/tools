// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
