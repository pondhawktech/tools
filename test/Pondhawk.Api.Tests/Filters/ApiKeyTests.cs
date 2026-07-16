// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Pondhawk.Api.Filters;
using Shouldly;
using Xunit;

namespace Pondhawk.Api.Tests.Filters;

public class SimpleApiKeyValidatorTests
{
    [Fact]
    public void IsValid_CorrectKey_True()
    {
        var validator = new SimpleApiKeyValidator("secret-key");
        validator.IsValid("secret-key").ShouldBeTrue();
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_WrongOrEmpty_False(string candidate)
    {
        var validator = new SimpleApiKeyValidator("secret-key");
        validator.IsValid(candidate).ShouldBeFalse();
    }
}

public class ApiKeyEndpointFilterTests
{
    private static EndpointFilterInvocationContext Context(string apiKey = null)
    {
        var http = new DefaultHttpContext();
        if (apiKey is not null)
            http.Request.Headers[ApiKeyEndpointFilter.HeaderName] = apiKey;
        return EndpointFilterInvocationContext.Create(http);
    }

    [Fact]
    public async Task MissingHeader_Returns401()
    {
        var filter = new ApiKeyEndpointFilter(new SimpleApiKeyValidator("k"));
        var nextCalled = false;
        EndpointFilterDelegate next = _ => { nextCalled = true; return ValueTask.FromResult<object>("ok"); };

        var result = await filter.InvokeAsync(Context(), next);

        nextCalled.ShouldBeFalse();
        result.ShouldBeAssignableTo<IResult>();
        await AssertStatus(result, 401);
    }

    [Fact]
    public async Task InvalidKey_Returns401()
    {
        var filter = new ApiKeyEndpointFilter(new SimpleApiKeyValidator("k"));
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object>("ok");

        var result = await filter.InvokeAsync(Context("wrong"), next);

        await AssertStatus(result, 401);
    }

    [Fact]
    public async Task ValidKey_CallsNext()
    {
        var filter = new ApiKeyEndpointFilter(new SimpleApiKeyValidator("k"));
        var nextCalled = false;
        EndpointFilterDelegate next = _ => { nextCalled = true; return ValueTask.FromResult<object>("passed"); };

        var result = await filter.InvokeAsync(Context("k"), next);

        nextCalled.ShouldBeTrue();
        result.ShouldBe("passed");
    }

    private static async Task AssertStatus(object result, int expected)
    {
        var http = TestServices.HttpContext();
        await ((IResult)result).ExecuteAsync(http);
        http.Response.StatusCode.ShouldBe(expected);
    }
}
