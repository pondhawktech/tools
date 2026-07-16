// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Pondhawk.Api.Endpoints;
using Shouldly;
using Xunit;

namespace Pondhawk.Api.Tests.Endpoints;

public sealed class GoodModule : IEndpointModule
{
    public string BasePath => "/good";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/ping", () => Results.Ok("pong"));
    }
}

public sealed class ThrowingModule : IEndpointModule
{
    public string BasePath => "/bad";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        throw new InvalidOperationException("module blew up");
    }
}

public class EndpointExtensionsTests
{
    [Fact]
    public void AddEndpointModules_RegistersModulesFromAssembly()
    {
        var services = new ServiceCollection();
        services.AddEndpointModules(typeof(GoodModule).Assembly);

        var provider = services.BuildServiceProvider();
        var modules = provider.GetServices<IEndpointModule>().ToList();

        modules.ShouldContain(m => m is GoodModule);
        modules.ShouldContain(m => m is ThrowingModule);
    }

    [Fact]
    public void MapEndpointModules_IsResilientToThrowingModule()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IEndpointModule>(new GoodModule());
        builder.Services.AddSingleton<IEndpointModule>(new ThrowingModule());
        var app = builder.Build();

        Should.NotThrow(() => app.MapEndpointModules("/api"));

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText)
            .ToList();

        endpoints.ShouldContain(p => p.Contains("/good/ping", StringComparison.Ordinal));
    }
}
