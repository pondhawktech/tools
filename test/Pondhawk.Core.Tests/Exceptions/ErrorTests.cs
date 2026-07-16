// Copyright (c) Pond Hawk Technologies Inc. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Pondhawk.Exceptions;
using Shouldly;
using Xunit;

namespace Pondhawk.Core.Tests.Exceptions;

public class ErrorTests
{

    [Fact]
    public void Ok_HasNoneKind_EmptyCodeAndExplanation()
    {
        var ok = Error.Ok;

        ok.Kind.ShouldBe(ErrorKind.None);
        ok.ErrorCode.ShouldBe("");
        ok.Explanation.ShouldBe("");
        ok.Details.ShouldBeEmpty();
    }

    [Fact]
    public void NotFoundError_Create_SetsNotFoundKindAndCode()
    {
        var error = NotFoundError.Create("Resource missing");

        error.Kind.ShouldBe(ErrorKind.NotFound);
        error.ErrorCode.ShouldBe("Not Found");
    }

    [Fact]
    public void NotFoundError_Create_SetsExplanation()
    {
        var error = NotFoundError.Create("Order 123 not found");

        error.Explanation.ShouldBe("Order 123 not found");
    }

    [Fact]
    public void NotValidError_Create_SetsPredicateKindAndValidationFailureCode()
    {
        var violations = new List<EventDetail>
        {
            EventDetail.Build().WithExplanation("Name is required")
        };

        var error = NotValidError.Create(violations, "OrderValidation");

        error.Kind.ShouldBe(ErrorKind.Predicate);
        error.ErrorCode.ShouldBe("ValidationFailure");
    }

    [Fact]
    public void NotValidError_Create_WithContext_IncludesContextInExplanation()
    {
        var violations = new List<EventDetail>();

        var error = NotValidError.Create(violations, "OrderValidation");

        error.Explanation.ShouldBe("Validation errors exist. OrderValidation");
    }

    [Fact]
    public void NotValidError_Create_NullContext_UsesDefaultContext()
    {
        var violations = new List<EventDetail>();

        var error = NotValidError.Create(violations, null);

        error.Explanation.ShouldBe("Validation errors exist. No context available");
    }

    [Fact]
    public void NotValidError_Create_CopiesViolationsToDetails()
    {
        var violations = new List<EventDetail>
        {
            EventDetail.Build().WithExplanation("Name is required"),
            EventDetail.Build().WithExplanation("Age must be positive")
        };

        var error = NotValidError.Create(violations, "Test");

        error.Details.Count().ShouldBe(2);
        error.Details.First().Explanation.ShouldBe("Name is required");
        error.Details.Last().Explanation.ShouldBe("Age must be positive");
    }

    [Fact]
    public void UnhandledError_Create_SetsSystemKind()
    {
        var ex = new InvalidOperationException("bad state");

        var error = UnhandledError.Create(ex, "Processing");

        error.Kind.ShouldBe(ErrorKind.System);
    }

    [Fact]
    public void UnhandledError_Create_ExtractsErrorCodeFromExceptionTypeName()
    {
        var ex = new InvalidOperationException("bad state");

        var error = UnhandledError.Create(ex);

        error.ErrorCode.ShouldBe("InvalidOperation");
    }

    [Fact]
    public void UnhandledError_Create_BareException_UsesExceptionAsCode()
    {
        var ex = new Exception("generic");

        var error = UnhandledError.Create(ex);

        error.ErrorCode.ShouldBe("Exception");
    }

    [Fact]
    public void UnhandledError_Create_WithContext_IncludesContextInExplanation()
    {
        var ex = new Exception("fail");

        var error = UnhandledError.Create(ex, "OrderService.Process");

        error.Explanation.ShouldBe("An unhandled exception was caught. OrderService.Process");
    }

    [Fact]
    public void UnhandledError_Create_NullContext_UsesDefaultContext()
    {
        var ex = new Exception("fail");

        var error = UnhandledError.Create(ex, null);

        error.Explanation.ShouldBe("An unhandled exception was caught. No context available");
    }

}
