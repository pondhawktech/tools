using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Pondhawk.Api.Identity;
using Shouldly;
using Xunit;

namespace Pondhawk.Api.Tests.Identity;

public class AuthenticationHandlerTests
{
    private static AuthenticationScheme SchemeFor(Type handlerType) =>
        new(IdentityConstants.Scheme, null, handlerType);

    private static async Task<GatewayTokenAuthenticationHandler> CreateTokenHandler(HttpContext http, IGatewayTokenEncoder encoder)
    {
        var handler = new GatewayTokenAuthenticationHandler(
            encoder,
            new TestOptionsMonitor<GatewayAuthenticationSchemeOptions>(new GatewayAuthenticationSchemeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default);

        await handler.InitializeAsync(SchemeFor(typeof(GatewayTokenAuthenticationHandler)), http);
        return handler;
    }

    private static async Task<GatewayHeaderAuthenticationHandler> CreateHeaderHandler(HttpContext http)
    {
        var handler = new GatewayHeaderAuthenticationHandler(
            new TestOptionsMonitor<GatewayAuthenticationSchemeOptions>(new GatewayAuthenticationSchemeOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default);

        await handler.InitializeAsync(SchemeFor(typeof(GatewayHeaderAuthenticationHandler)), http);
        return handler;
    }

    // ---- Token handler ----

    [Fact]
    public async Task Token_MissingHeader_ReturnsNoResult()
    {
        var http = new DefaultHttpContext();
        var handler = await CreateTokenHandler(http, new GatewayTokenJwtEncoder(TestKeys.SigningKey));

        var result = await handler.AuthenticateAsync();

        result.None.ShouldBeTrue();
    }

    [Fact]
    public async Task Token_EmptyHeader_ReturnsNoResult()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers[IdentityConstants.TokenHeaderName] = "";
        var handler = await CreateTokenHandler(http, new GatewayTokenJwtEncoder(TestKeys.SigningKey));

        var result = await handler.AuthenticateAsync();

        result.None.ShouldBeTrue();
    }

    [Fact]
    public async Task Token_ValidToken_Succeeds()
    {
        var encoder = new GatewayTokenJwtEncoder(TestKeys.SigningKey);
        var token = encoder.Encode(TestKeys.SampleClaimSet());

        var http = new DefaultHttpContext();
        http.Request.Headers[IdentityConstants.TokenHeaderName] = token;
        var handler = await CreateTokenHandler(http, encoder);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeTrue();
        result.Principal.FindFirst(IdentityConstants.SubjectClaim).Value.ShouldBe("u-123");
        result.Principal.IsInRole("admin").ShouldBeTrue();
    }

    [Fact]
    public async Task Token_InvalidToken_Fails()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers[IdentityConstants.TokenHeaderName] = "garbage-token";
        var handler = await CreateTokenHandler(http, new GatewayTokenJwtEncoder(TestKeys.SigningKey));

        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
    }

    // ---- Header handler ----

    [Fact]
    public async Task Header_MissingHeader_ReturnsNoResult()
    {
        var http = new DefaultHttpContext();
        var handler = await CreateHeaderHandler(http);

        var result = await handler.AuthenticateAsync();

        result.None.ShouldBeTrue();
    }

    [Fact]
    public async Task Header_WhitespaceHeader_ReturnsNoResult()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers[IdentityConstants.IdentityHeaderName] = "   ";
        var handler = await CreateHeaderHandler(http);

        var result = await handler.AuthenticateAsync();

        result.None.ShouldBeTrue();
    }

    [Fact]
    public async Task Header_ValidJson_Succeeds()
    {
        var json = JsonSerializer.Serialize(TestKeys.SampleClaimSet());

        var http = new DefaultHttpContext();
        http.Request.Headers[IdentityConstants.IdentityHeaderName] = json;
        var handler = await CreateHeaderHandler(http);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeTrue();
        result.Principal.FindFirst(IdentityConstants.EmailClaim).Value.ShouldBe("jane@example.com");
    }

    [Fact]
    public async Task Header_MalformedJson_Fails()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers[IdentityConstants.IdentityHeaderName] = "{ not json ";
        var handler = await CreateHeaderHandler(http);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
    }

    [Fact]
    public async Task Header_JsonNull_ReturnsNoResult()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers[IdentityConstants.IdentityHeaderName] = "null";
        var handler = await CreateHeaderHandler(http);

        var result = await handler.AuthenticateAsync();

        result.None.ShouldBeTrue();
    }
}
