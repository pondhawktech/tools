// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Pondhawk.Exceptions;

/// <summary>
/// Represents a structured error with a kind, code, explanation, and optional detail events.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Error is a well-established domain type name")]
public class Error
{

    /// <summary>
    /// A shared instance representing a successful (no-error) result.
    /// </summary>
    public static readonly Error Ok = new() { Kind = ErrorKind.None, ErrorCode = "", Explanation = "", Details = [] };

    /// <summary>
    /// Gets the classification of this error.
    /// </summary>
    public ErrorKind Kind { get; init; }

    /// <summary>
    /// Gets the error code identifying the type of error.
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Gets the human-readable explanation of the error.
    /// </summary>
    public string Explanation { get; init; } = string.Empty;

    /// <summary>
    /// Gets the event details associated with this error.
    /// </summary>
    public IEnumerable<EventDetail> Details { get; init; } = [];

}
