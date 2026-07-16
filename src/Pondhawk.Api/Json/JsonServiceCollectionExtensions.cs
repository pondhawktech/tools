// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Pondhawk.Api.Json;

/// <summary>
/// JSON configuration for the Pondhawk.Api web kit (the Microsoft.Extensions.DI replacement for
/// Fabrica's Autofac helper).
/// </summary>
public static class JsonServiceCollectionExtensions
{
    /// <summary>
    /// Builds a <see cref="JsonSerializerOptions"/> from Web defaults + the compact resolver, lets the
    /// caller customize it, applies it to minimal-API <see cref="JsonOptions"/>, and registers the
    /// options instance as a singleton (so filters/handlers can resolve it).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional customization of the options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPondhawkJson(this IServiceCollection services, Action<JsonSerializerOptions>? configure = null)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new CompactJsonTypeInfoResolver(),
        };

        configure?.Invoke(options);

        services.Configure<JsonOptions>(o => CopyInto(options, o.SerializerOptions));
        services.AddSingleton(options);

        return services;
    }

    private static void CopyInto(JsonSerializerOptions from, JsonSerializerOptions to)
    {
        to.PropertyNamingPolicy = from.PropertyNamingPolicy;
        to.DictionaryKeyPolicy = from.DictionaryKeyPolicy;
        to.DefaultIgnoreCondition = from.DefaultIgnoreCondition;
        to.ReferenceHandler = from.ReferenceHandler;
        to.PropertyNameCaseInsensitive = from.PropertyNameCaseInsensitive;
        to.UnmappedMemberHandling = from.UnmappedMemberHandling;
        to.WriteIndented = from.WriteIndented;
        to.TypeInfoResolver = from.TypeInfoResolver;
    }
}
