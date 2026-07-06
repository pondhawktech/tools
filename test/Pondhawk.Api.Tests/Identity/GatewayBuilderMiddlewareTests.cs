using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Pondhawk.Api.Identity;
using Shouldly;
using Xunit;

namespace Pondhawk.Api.Tests.Identity;

public class GatewayTokenBuilderMiddlewareTests
{
    [Fact]
    public async Task Authenticated_WritesTokenHeader()
    {
        var encoder = new GatewayTokenJwtEncoder(TestKeys.SigningKey);
        var nextCalled = false;
        var middleware = new GatewayTokenBuilderMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        var http = new DefaultHttpContext();
        http.User = ClaimSetPrincipal.ToPrincipal(TestKeys.SampleClaimSet(), IdentityConstants.Scheme);

        await middleware.Invoke(http, encoder);

        nextCalled.ShouldBeTrue();
        var token = http.Request.Headers[IdentityConstants.TokenHeaderName].ToString();
        token.ShouldNotBeNullOrWhiteSpace();

        var decoded = await encoder.DecodeAsync(token);
        decoded.UserId.ShouldBe("u-123");
    }

    [Fact]
    public async Task Unauthenticated_LeavesNoTokenHeader()
    {
        var encoder = new GatewayTokenJwtEncoder(TestKeys.SigningKey);
        var middleware = new GatewayTokenBuilderMiddleware(_ => Task.CompletedTask);

        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity()); // not authenticated
        http.Request.Headers[IdentityConstants.TokenHeaderName] = "stale";

        await middleware.Invoke(http, encoder);

        http.Request.Headers.ContainsKey(IdentityConstants.TokenHeaderName).ShouldBeFalse();
    }
}

public class GatewayHeaderBuilderMiddlewareTests
{
    [Fact]
    public async Task Authenticated_WritesIdentityHeader()
    {
        var middleware = new GatewayHeaderBuilderMiddleware(_ => Task.CompletedTask);

        var http = new DefaultHttpContext();
        http.User = ClaimSetPrincipal.ToPrincipal(TestKeys.SampleClaimSet(), IdentityConstants.Scheme);

        await middleware.Invoke(http);

        var json = http.Request.Headers[IdentityConstants.IdentityHeaderName].ToString();
        json.ShouldNotBeNullOrWhiteSpace();
        var claims = JsonSerializer.Deserialize<ClaimSet>(json);
        claims.Email.ShouldBe("jane@example.com");
    }

    [Fact]
    public async Task Unauthenticated_LeavesNoIdentityHeader()
    {
        var middleware = new GatewayHeaderBuilderMiddleware(_ => Task.CompletedTask);

        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity());

        await middleware.Invoke(http);

        http.Request.Headers.ContainsKey(IdentityConstants.IdentityHeaderName).ShouldBeFalse();
    }
}

public class GatewayTokenHttpHandlerTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class FakeTokenSource : IAccessTokenSource
    {
        private readonly string _token;
        public FakeTokenSource(string token) => _token = token;
        public string Name => "fake";
        public bool HasExpired => false;
        public Task<string> GetTokenAsync() => Task.FromResult(_token);
    }

    [Fact]
    public async Task AttachesTokenHeader_WhenTokenPresent()
    {
        var stub = new StubHandler();
        var handler = new GatewayTokenHttpHandler(new FakeTokenSource("outbound-token")) { InnerHandler = stub };
        var invoker = new HttpMessageInvoker(handler);

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://svc/api"), CancellationToken.None);

        stub.LastRequest.Headers.GetValues(IdentityConstants.TokenHeaderName).ShouldContain("outbound-token");
    }

    [Fact]
    public async Task NoHeader_WhenTokenEmpty()
    {
        var stub = new StubHandler();
        var handler = new GatewayTokenHttpHandler(new FakeTokenSource("")) { InnerHandler = stub };
        var invoker = new HttpMessageInvoker(handler);

        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://svc/api"), CancellationToken.None);

        stub.LastRequest.Headers.Contains(IdentityConstants.TokenHeaderName).ShouldBeFalse();
    }

    [Fact]
    public async Task NullRequest_Throws()
    {
        var handler = new GatewayTokenHttpHandler(new FakeTokenSource("t")) { InnerHandler = new StubHandler() };
        var invoker = new HttpMessageInvoker(handler);

        await Should.ThrowAsync<ArgumentNullException>(() => invoker.SendAsync(null, CancellationToken.None));
    }
}
