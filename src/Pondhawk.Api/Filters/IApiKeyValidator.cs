// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Pondhawk.Api.Filters;

/// <summary>Validates a candidate API key in constant time.</summary>
public interface IApiKeyValidator
{
    /// <summary>Returns <see langword="true"/> if <paramref name="candidate"/> is the expected key.</summary>
    /// <param name="candidate">The presented key.</param>
    bool IsValid(string candidate);
}
