using Pondhawk.Exceptions;
using Pondhawk.Mediator;
using Shouldly;
using Xunit;

namespace Pondhawk.Core.Tests.Mediator;

public class ResponseTests
{

    [Fact]
    public void ImplicitConversion_FromValue_ProducesSuccess()
    {
        Response<int> response = 42;

        response.Ok.ShouldBeTrue();
        response.Value.ShouldBe(42);
        response.Error.ShouldBeNull();
    }

    [Fact]
    public void ImplicitConversion_IsEquivalentToSuccess()
    {
        Response<string> implicitly = "hello";
        var explicitly = Response<string>.Success("hello");

        implicitly.ShouldBe(explicitly);
    }

    [Fact]
    public void Failure_SetsErrorAndNotOk()
    {
        var error = new ErrorInfo { Kind = ErrorKind.NotFound, ErrorCode = "NotFound", Explanation = "gone" };

        var response = Response<string>.Failure(error);

        response.Ok.ShouldBeFalse();
        response.Value.ShouldBeNull();
        response.Error.ShouldBeSameAs(error);
    }

    [Fact]
    public void Match_RoutesByOutcome()
    {
        Response<int> ok = 7;
        var failed = Response<int>.Failure(new ErrorInfo { Kind = ErrorKind.System, ErrorCode = "System", Explanation = "boom" });

        ok.Match(v => $"ok:{v}", e => $"err:{e.Kind}").ShouldBe("ok:7");
        failed.Match(v => $"ok:{v}", e => $"err:{e.Kind}").ShouldBe("err:System");
    }

    [Fact]
    public void GetValueOrThrow_OnFailure_Throws()
    {
        var failed = Response<int>.Failure(new ErrorInfo { Kind = ErrorKind.Conflict, ErrorCode = "Conflict", Explanation = "nope" });

        Should.Throw<InvalidOperationException>(() => failed.GetValueOrThrow());
    }

    [Fact]
    public void Receipt_One_AffectsOne()
    {
        Receipt.One.Affected.ShouldBe(1);
    }

    [Fact]
    public void Receipt_Of_CarriesTally()
    {
        Receipt.Of(5).Affected.ShouldBe(5);
    }

    [Fact]
    public void IsError_IsInverseOfOk()
    {
        Response<int> ok = 1;
        var failed = Response<int>.Failure(new ErrorInfo { Kind = ErrorKind.System, ErrorCode = "System", Explanation = "boom" });

        ok.IsError.ShouldBe(!ok.Ok);
        ok.IsError.ShouldBeFalse();
        failed.IsError.ShouldBe(!failed.Ok);
        failed.IsError.ShouldBeTrue();
    }

    [Fact]
    public void AsEntity_OnSuccess_ReturnsValue()
    {
        Response<int> ok = 42;

        ok.AsEntity.ShouldBe(42);
    }

    [Fact]
    public void AsEntity_OnFailure_Throws()
    {
        var failed = Response<int>.Failure(new ErrorInfo { Kind = ErrorKind.NotFound, ErrorCode = "NotFound", Explanation = "gone" });

        Should.Throw<InvalidOperationException>(() => failed.AsEntity);
    }

    [Fact]
    public void AsError_OnFailure_ReturnsErrorInfo()
    {
        var error = new ErrorInfo { Kind = ErrorKind.Conflict, ErrorCode = "Conflict", Explanation = "nope" };
        var failed = Response<int>.Failure(error);

        failed.AsError.ShouldBeSameAs(error);
    }

    [Fact]
    public void AsError_OnSuccess_Throws()
    {
        Response<int> ok = 1;

        Should.Throw<InvalidOperationException>(() => ok.AsError);
    }

    [Fact]
    public void ImplicitConversion_FromErrorInfo_ProducesFailure()
    {
        var error = new ErrorInfo { Kind = ErrorKind.NotFound, ErrorCode = "NotFound", Explanation = "gone" };

        Response<string> response = error;

        response.Ok.ShouldBeFalse();
        response.IsError.ShouldBeTrue();
        response.Error.ShouldBeSameAs(error);
    }

    [Fact]
    public void AsError_PropagatesAcrossPayloadTypes()
    {
        var error = new ErrorInfo { Kind = ErrorKind.NotFound, ErrorCode = "NotFound", Explanation = "gone" };
        var failed = Response<int>.Failure(error);

        static Response<string> Propagate(Response<int> source) => source.AsError;

        var propagated = Propagate(failed);

        propagated.IsError.ShouldBeTrue();
        propagated.Error.ShouldBeSameAs(error);
    }

}
