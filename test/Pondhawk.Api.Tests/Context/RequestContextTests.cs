using System.Security.Claims;
using Pondhawk.Api.Context;
using Pondhawk.Logging;
using Shouldly;
using Xunit;

namespace Pondhawk.Api.Tests.Context;

public class RequestContextTests
{
    private static IRequestContext NewContext() =>
        (IRequestContext)Activator.CreateInstance(
            typeof(IRequestContext).Assembly.GetType("Pondhawk.Api.Context.RequestContext"), nonPublic: true);

    [Fact]
    public void Caller_And_Token_GetSet()
    {
        var ctx = NewContext();
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        ctx.Caller = principal;
        ctx.CallerGatewayToken = "tok";

        ctx.Caller.ShouldBeSameAs(principal);
        ctx.CallerGatewayToken.ShouldBe("tok");
    }

    [Fact]
    public void CorrelationId_GeneratesStableId_WhenNoActivity()
    {
        var ctx = NewContext();

        var id = ctx.CorrelationId;

        id.ShouldNotBeNullOrWhiteSpace();
        ctx.CorrelationId.ShouldBe(id); // stable across reads
    }

    [Fact]
    public void CorrelationId_ReflectsCorrelationManager()
    {
        var ctx = NewContext();
        using (CorrelationManager.Begin("my-correlation"))
        {
            ctx.CorrelationId.ShouldBe("my-correlation");
        }
    }
}
