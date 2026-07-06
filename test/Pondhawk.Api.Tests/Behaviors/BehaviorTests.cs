using Pondhawk.Api.Behaviors;
using Pondhawk.Exceptions;
using Pondhawk.Mediator;
using Pondhawk.Rules;
using Pondhawk.Rules.Factory;
using Shouldly;
using Xunit;

namespace Pondhawk.Api.Tests.Behaviors;

public sealed class TestRequest : IRequest<string>
{
    public string Name { get; set; } = string.Empty;
}

public class ValidationBehaviorTests
{
    [Fact]
    public async Task NoRuleSets_PassesThrough()
    {
        var behavior = new ValidationBehavior<TestRequest, string>(Array.Empty<IRuleSet>());

        var result = await behavior.HandleAsync(new TestRequest { Name = "ok" }, () => Task.FromResult("value"));

        result.ShouldBe("value");
    }

    [Fact]
    public async Task NoViolations_PassesThrough()
    {
        var ruleSet = new RuleSet();
        ruleSet.AddValidation<TestRequest>("name-required")
            .Assert<string>(r => r.Name)
            .Is((r, v) => !string.IsNullOrWhiteSpace(v))
            .Otherwise("Name is required");

        var behavior = new ValidationBehavior<TestRequest, string>(new IRuleSet[] { ruleSet });

        var result = await behavior.HandleAsync(new TestRequest { Name = "present" }, () => Task.FromResult("value"));

        result.ShouldBe("value");
    }

    [Fact]
    public async Task Violation_ThrowsFailedValidationException()
    {
        var ruleSet = new RuleSet();
        ruleSet.AddValidation<TestRequest>("name-required")
            .Assert<string>(r => r.Name)
            .Is((r, v) => !string.IsNullOrWhiteSpace(v))
            .Otherwise("Name is required");

        var behavior = new ValidationBehavior<TestRequest, string>(new IRuleSet[] { ruleSet });

        var ex = await Should.ThrowAsync<FailedValidationException>(() =>
            behavior.HandleAsync(new TestRequest { Name = "" }, () => Task.FromResult("value")));

        ex.Kind.ShouldBe(ErrorKind.Predicate);
        ex.Details.ShouldNotBeEmpty();
    }
}

public class LoggingBehaviorTests
{
    [Fact]
    public async Task PassesThroughNextValue()
    {
        var behavior = new LoggingBehavior<TestRequest, string>(
            new FakeRequestContext { CorrelationId = "abc" });

        var result = await behavior.HandleAsync(new TestRequest(), () => Task.FromResult("piped"));

        result.ShouldBe("piped");
    }
}
