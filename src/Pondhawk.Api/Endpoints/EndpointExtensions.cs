// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Pondhawk.Logging.Utilities;
using Serilog;

namespace Pondhawk.Api.Endpoints;

/// <summary>
/// Registration and mapping for <see cref="IEndpointModule"/>s.
/// </summary>
public static class EndpointExtensions
{
    /// <summary>
    /// Discovers concrete <see cref="IEndpointModule"/> implementations in the given assemblies and
    /// registers each as a singleton, so modules can take constructor dependencies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="sources">The assemblies to scan.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEndpointModules(this IServiceCollection services, params Assembly[] sources)
    {
        var modules = sources
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t != typeof(IEndpointModule) && typeof(IEndpointModule).IsAssignableFrom(t));

        foreach (var module in modules)
            services.AddSingleton(typeof(IEndpointModule), module);

        return services;
    }

    /// <summary>
    /// Maps every registered <see cref="IEndpointModule"/> under <paramref name="basePath"/>, giving
    /// each its own route group at its <see cref="IEndpointModule.BasePath"/>. A module that throws
    /// while mapping is logged and skipped, so one bad module cannot break startup.
    /// </summary>
    /// <param name="builder">The endpoint route builder.</param>
    /// <param name="basePath">The root path all modules are grouped under.</param>
    /// <param name="configureRoot">Optional configuration for the root group.</param>
    /// <returns>The route builder for chaining.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Resilient module loading: one failing module must not abort startup; it is logged and skipped.")]
    public static IEndpointRouteBuilder MapEndpointModules(
        this IEndpointRouteBuilder builder,
        string basePath = "",
        Action<RouteGroupBuilder>? configureRoot = null)
    {
        var logger = Log.ForContext(typeof(EndpointExtensions));

        var root = builder.MapGroup(basePath);
        configureRoot?.Invoke(root);

        foreach (var module in builder.ServiceProvider.GetServices<IEndpointModule>())
        {
            var name = module.GetType().GetConciseName();
            try
            {
                logger.Debug("Loading endpoint module {Module}", name);

                var group = root.MapGroup(module.BasePath);
                module.Configure(group);
                module.AddRoutes(group);
            }
            catch (Exception cause)
            {
                logger.Error(cause, "Endpoint module {Module} failed to load", name);
            }
        }

        return builder;
    }
}
