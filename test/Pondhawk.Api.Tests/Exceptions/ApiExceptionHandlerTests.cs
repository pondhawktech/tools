// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Pondhawk.Api.Context;
using Pondhawk.Api.Exceptions;
using Pondhawk.Exceptions;
using Shouldly;
using Xunit;

namespace Pondhawk.Api.Tests.Exceptions;

public class ApiExceptionHandlerTests
{
    private static async Task<(bool Handled, int Status, string ContentType, string Body)> Handle(Exception ex)
    {
        var handler = new ApiExceptionHandler();
        var services = new ServiceCollection();
        services.AddScoped<IRequestContext>(_ => new FakeRequestContext { CorrelationId = "corr-x" });
        var http = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };
        http.Request.Path = "/api/resource";

        var handled = await handler.TryHandleAsync(http, ex, CancellationToken.None);

        http.Response.Body.Position = 0;
        using var reader = new StreamReader(http.Response.Body);
        var body = await reader.ReadToEndAsync();
        return (handled, http.Response.StatusCode, http.Response.ContentType, body);
    }

    [Fact]
    public async Task ExternalException_UsesKindStatus()
    {
        var (handled, status, contentType, body) = await Handle(new NotFoundException("Order", 42));

        handled.ShouldBeTrue();
        status.ShouldBe(404);
        contentType.ShouldBe("application/problem+json");
        body.ShouldContain("corr-x");
    }

    [Fact]
    public async Task ConflictException_Returns409()
    {
        var (handled, status, _, _) = await Handle(new ConflictException("already exists"));

        handled.ShouldBeTrue();
        status.ShouldBe(409);
    }

    [Fact]
    public async Task JsonException_Returns400()
    {
        var (handled, status, contentType, body) = await Handle(new JsonException("bad json"));

        handled.ShouldBeTrue();
        status.ShouldBe(400);
        contentType.ShouldBe("application/problem+json");
        body.ShouldContain("BadJsonRequest");
    }

    [Fact]
    public async Task GenericException_Returns500()
    {
        var (handled, status, _, body) = await Handle(new InvalidOperationException("kaboom"));

        handled.ShouldBeTrue();
        status.ShouldBe(500);
        body.ShouldContain("kaboom");
    }
}
