// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Pondhawk.Api.Filters;

/// <summary>
/// Endpoint filter that requires a valid <c>x-api-key</c> header; otherwise returns <c>401</c>.
/// </summary>
public sealed class ApiKeyEndpointFilter(IApiKeyValidator validator) : IEndpointFilter
{
    /// <summary>The header carrying the API key.</summary>
    public const string HeaderName = "x-api-key";

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var values)
            || values.Count == 0
            || !validator.IsValid(values[0] ?? string.Empty))
        {
            return Results.Unauthorized();
        }

        return await next(context).ConfigureAwait(false);
    }
}
