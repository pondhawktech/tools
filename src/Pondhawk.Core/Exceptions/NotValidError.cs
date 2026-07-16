// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Pondhawk.Exceptions;

/// <summary>
/// An error indicating validation failures, carrying the associated violation details.
/// </summary>
public class NotValidError : Error
{

    /// <summary>
    /// Creates a <see cref="NotValidError"/> from the specified violations and optional context.
    /// </summary>
    /// <param name="violations">The validation violations that produced this error.</param>
    /// <param name="context">Optional context describing where the validation failed.</param>
    /// <returns>A new <see cref="NotValidError"/> instance.</returns>
    public static NotValidError Create(IEnumerable<EventDetail> violations, string? context = null)
    {

        if (string.IsNullOrWhiteSpace(context))
            context = "No context available";

        var error = new NotValidError
        {
            Kind = ErrorKind.Predicate,
            ErrorCode = "ValidationFailure",
            Explanation = $"Validation errors exist. {context}",
            Details = [.. violations]
        };

        return error;

    }

}
