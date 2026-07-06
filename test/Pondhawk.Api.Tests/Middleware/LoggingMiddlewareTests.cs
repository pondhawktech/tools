using System.Text;
using Microsoft.AspNetCore.Http;
using Pondhawk.Api.Identity;
using Pondhawk.Api.Middleware;
using Shouldly;
using Xunit;

namespace Pondhawk.Api.Tests.Middleware;

public class DiagnosticsMonitorMiddlewareTests
{
    [Fact]
    public async Task Invoke_CallsNext_AndLogsAroundIt()
    {
        var nextCalled = false;
        var middleware = new DiagnosticsMonitorMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = "/x";

        await middleware.Invoke(http, new FakeRequestContext { CorrelationId = "c1" });

        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task Invoke_LogsEnd_EvenWhenNextThrows()
    {
        var middleware = new DiagnosticsMonitorMiddleware(_ => throw new InvalidOperationException("boom"));

        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/y";

        await Should.ThrowAsync<InvalidOperationException>(() =>
            middleware.Invoke(http, new FakeRequestContext()));
    }
}

public class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task Invoke_WithBodyAndHeaders_BuffersAndCallsNext()
    {
        var nextCalled = false;
        var middleware = new RequestLoggingMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/submit";
        http.Request.QueryString = new QueryString("?a=1");
        http.Request.Headers["Authorization"] = "Bearer sometoken";
        http.Request.Headers[IdentityConstants.TokenHeaderName] = "gw";
        http.Request.Headers["Cookie"] = "sid=abc";
        http.Request.Headers["X-Normal"] = "visible";

        var bytes = Encoding.UTF8.GetBytes("{\"hello\":\"world\"}");
        http.Request.Body = new MemoryStream(bytes);
        http.Request.ContentLength = bytes.Length;

        await middleware.Invoke(http);

        nextCalled.ShouldBeTrue();
        // Body was rewound so downstream can read it again.
        http.Request.Body.Position.ShouldBe(0);
    }

    [Fact]
    public async Task Invoke_NoBody_StillCallsNext()
    {
        var nextCalled = false;
        var middleware = new RequestLoggingMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = "/ping";

        await middleware.Invoke(http);

        nextCalled.ShouldBeTrue();
    }
}
