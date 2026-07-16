// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Routing;

namespace Pondhawk.Api.Endpoints;

/// <summary>
/// A self-contained group of minimal-API routes. Implementations are discovered from an assembly,
/// activated through DI, and mapped under an optional per-module <see cref="BasePath"/>.
/// </summary>
public interface IEndpointModule
{
    /// <summary>Gets the route prefix this module's endpoints are grouped under (relative to the app base path).</summary>
    string BasePath => string.Empty;

    /// <summary>Configures the module's route group (e.g. shared filters, auth, metadata). Optional.</summary>
    /// <param name="group">The route group for this module.</param>
    void Configure(RouteGroupBuilder group)
    {
    }

    /// <summary>Maps the module's routes.</summary>
    /// <param name="app">The route builder (the module's group).</param>
    void AddRoutes(IEndpointRouteBuilder app);
}
