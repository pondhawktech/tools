using Pondhawk.Api.Exceptions;
using Pondhawk.Exceptions;
using Shouldly;
using Xunit;

namespace Pondhawk.Api.Tests.Exceptions;

public class ErrorStatusMapTests
{
    [Theory]
    [InlineData(ErrorKind.None, 200)]
    [InlineData(ErrorKind.NotFound, 404)]
    [InlineData(ErrorKind.NotImplemented, 501)]
    [InlineData(ErrorKind.Predicate, 422)]
    [InlineData(ErrorKind.Conflict, 409)]
    [InlineData(ErrorKind.Concurrency, 410)]
    [InlineData(ErrorKind.BadRequest, 400)]
    [InlineData(ErrorKind.AuthenticationRequired, 401)]
    [InlineData(ErrorKind.NotAuthorized, 403)]
    [InlineData(ErrorKind.Remote, 502)]
    [InlineData(ErrorKind.System, 500)]
    [InlineData(ErrorKind.Functional, 500)]
    [InlineData(ErrorKind.Unknown, 500)]
    public void ToStatus_MapsEveryKind(ErrorKind kind, int expected)
    {
        ErrorStatusMap.ToStatus(kind).ShouldBe(expected);
    }
}
