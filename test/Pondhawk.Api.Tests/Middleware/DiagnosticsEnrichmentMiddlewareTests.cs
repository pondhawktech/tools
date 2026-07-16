// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Pondhawk.Api.Identity;
using Pondhawk.Api.Middleware;
using Shouldly;
using Xunit;

namespace Pondhawk.Api.Tests.Middleware;

public class DiagnosticsEnrichmentMiddlewareTests
{
    [Fact]
    public async Task Invoke_SetsCallerAndToken_AndCallsNext()
    {
        var nextCalled = false;
        var middleware = new DiagnosticsEnrichmentMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity("test"));
        http.Request.Headers[IdentityConstants.TokenHeaderName] = "gw-token";
        http.Request.Headers[DiagnosticsEnrichmentMiddleware.CorrelationHeader] = "corr-in";

        var ctx = new FakeRequestContext();

        using var activity = new Activity("test").Start();
        await middleware.Invoke(http, ctx);

        nextCalled.ShouldBeTrue();
        ctx.Caller.ShouldBeSameAs(http.User);
        ctx.CallerGatewayToken.ShouldBe("gw-token");
    }

    [Fact]
    public async Task Invoke_NoTokenHeader_LeavesTokenNull()
    {
        var middleware = new DiagnosticsEnrichmentMiddleware(_ => Task.CompletedTask);

        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity());
        var ctx = new FakeRequestContext();

        await middleware.Invoke(http, ctx);

        ctx.Caller.ShouldBeSameAs(http.User);
        ctx.CallerGatewayToken.ShouldBeNull();
    }
}
