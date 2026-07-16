// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Pondhawk.Exceptions;

/// <summary>
/// An error wrapping an unhandled exception with an error code derived from the exception type.
/// </summary>
public class UnhandledError : Error
{

    /// <summary>
    /// Creates an <see cref="UnhandledError"/> from the specified exception and optional context.
    /// </summary>
    /// <param name="cause">The unhandled exception that triggered this error.</param>
    /// <param name="context">Optional context describing where the exception was caught.</param>
    /// <returns>A new <see cref="UnhandledError"/> instance.</returns>
    public static UnhandledError Create(Exception cause, string? context = null)
    {

        if (string.IsNullOrWhiteSpace(context))
            context = "No context available";

        var errorCode = cause.GetType().Name.Replace("Exception", "");
        if (string.IsNullOrWhiteSpace(errorCode))
            errorCode = "Exception";

        var error = new UnhandledError
        {
            Kind = ErrorKind.System,
            ErrorCode = errorCode,
            Explanation = $"An unhandled exception was caught. {context}"
        };

        return error;

    }

}
