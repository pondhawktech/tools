using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Pondhawk.Api.Filters;
using Pondhawk.Exceptions;
using Pondhawk.Mediator;
using Shouldly;
using Xunit;

namespace Pondhawk.Api.Tests.Filters;

public class ResponseEndpointFilterTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static ResponseEndpointFilter CreateFilter() =>
        new(new FakeRequestContext { CorrelationId = "corr-1" }, Options);

    private static EndpointFilterInvocationContext Context(string path = "/things/1")
    {
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        return EndpointFilterInvocationContext.Create(http);
    }

    private static async Task<(int Status, string ContentType, string Body)> Execute(object result)
    {
        var http = TestServices.HttpContext();
        await ((IResult)result).ExecuteAsync(http);
        http.Response.Body.Position = 0;
        using var reader = new StreamReader(http.Response.Body);
        var body = await reader.ReadToEndAsync();
        return (http.Response.StatusCode, http.Response.ContentType, body);
    }

    [Fact]
    public async Task Success_WithValue_Writes200Json()
    {
        var filter = CreateFilter();
        var payload = new { Name = "widget" };
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object>(Response<object>.Success(payload));

        var result = await filter.InvokeAsync(Context(), next);

        var (status, contentType, body) = await Execute(result);
        status.ShouldBe(200);
        contentType.ShouldContain("application/json");
        body.ShouldContain("widget");
    }

    [Fact]
    public async Task Success_NullValue_Writes200Ok()
    {
        var filter = CreateFilter();
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object>(Response<object>.Success(null));

        var result = await filter.InvokeAsync(Context(), next);

        var (status, _, _) = await Execute(result);
        status.ShouldBe(200);
    }

    [Fact]
    public async Task Success_StreamValue_ReturnsStreamResult()
    {
        var filter = CreateFilter();
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object>(Response<Stream>.Success(stream));

        var result = await filter.InvokeAsync(Context(), next);

        var (status, contentType, _) = await Execute(result);
        status.ShouldBe(200);
        contentType.ShouldBe("application/json");
    }

    [Theory]
    [InlineData(ErrorKind.NotFound, 404)]
    [InlineData(ErrorKind.Predicate, 422)]
    [InlineData(ErrorKind.Conflict, 409)]
    [InlineData(ErrorKind.NotAuthorized, 403)]
    [InlineData(ErrorKind.System, 500)]
    public async Task Failure_MapsToStatus(ErrorKind kind, int expected)
    {
        var filter = CreateFilter();
        var error = new ErrorInfo { Kind = kind, ErrorCode = "code", Explanation = "boom" };
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object>(Response<object>.Failure(error));

        var result = await filter.InvokeAsync(Context(), next);

        var (status, contentType, body) = await Execute(result);
        status.ShouldBe(expected);
        contentType.ShouldContain("application/problem+json");
        body.ShouldContain("boom");
        body.ShouldContain("corr-1");
    }

    [Fact]
    public async Task NonResponse_PassesThroughUnchanged()
    {
        var filter = CreateFilter();
        var sentinel = new object();
        EndpointFilterDelegate next = _ => ValueTask.FromResult(sentinel);

        var result = await filter.InvokeAsync(Context(), next);

        result.ShouldBeSameAs(sentinel);
    }
}
