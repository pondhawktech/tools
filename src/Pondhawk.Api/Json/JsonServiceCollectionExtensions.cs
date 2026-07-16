// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Pondhawk.Api.Json;

/// <summary>
/// JSON configuration for the Pondhawk.Api web kit (the Microsoft.Extensions.DI replacement for
/// Fabrica's Autofac helper).
/// </summary>
public static class JsonServiceCollectionExtensions
{
    /// <summary>
    /// Configures minimal-API JSON (<see cref="JsonOptions"/>) with the compact resolver, lets the
    /// caller customize it, and registers that <em>same</em> <see cref="JsonSerializerOptions"/>
    /// instance as a singleton — so the response filter, the exception handler, and the framework all
    /// serialize through one source of truth. (Previously the singleton and the framework options were
    /// two objects kept in sync by a partial copy, which silently dropped converters, custom encoders,
    /// number handling, etc.)
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional customization of the options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPondhawkJson(this IServiceCollection services, Action<JsonSerializerOptions>? configure = null)
    {
        // JsonOptions.SerializerOptions is already seeded with JsonSerializerDefaults.Web by the framework.
        services.Configure<JsonOptions>(o =>
        {
            o.SerializerOptions.TypeInfoResolver = new CompactJsonTypeInfoResolver();
            configure?.Invoke(o.SerializerOptions);
        });

        // Hand out the exact same options instance the framework uses for minimal-API JSON, so there is
        // only one JsonSerializerOptions in the app and nothing can drift.
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions);

        return services;
    }
}
