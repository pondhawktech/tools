using Pondhawk.Rules.Factory;
using Pondhawk.Rules.Validators;
using Shouldly;
using Xunit;

namespace Pondhawk.Rules.Tests;

public class ValidationTests
{

    /// <summary>
    /// Helper that evaluates without throwing ViolationsExistException or NoRulesEvaluatedException.
    /// The instance method Evaluate(params object[]) uses a default EvaluationContext which throws
    /// on violations. This helper creates a context with exceptions disabled.
    /// </summary>
    private static EvaluationResults EvaluateSafe(RuleSet ruleSet, params object[] facts)
    {
        var ctx = ruleSet.GetEvaluationContext();
        ctx.ThrowValidationException = false;
        ctx.ThrowNoRulesException = false;
        ctx.AddAllFacts(facts);
        return ruleSet.Evaluate(ctx);
    }


    // ========== Basic validation ==========

    [Fact]
    public void Validation_PassingCondition_NoViolations()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("name-check")
            .Assert<string>(p => p.Name)
            .Is((p, v) => !string.IsNullOrWhiteSpace(v))
            .Otherwise("Name is required");

        var person = new Person { Name = "Alice" };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeFalse();
    }

    [Fact]
    public void Validation_FailingCondition_CreatesViolation()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("name-check")
            .Assert<string>(p => p.Name)
            .Is((p, v) => !string.IsNullOrWhiteSpace(v))
            .Otherwise("Name is required");

        var person = new Person { Name = "" };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeTrue();
        result.Events.Count.ShouldBe(1);
        result.Events.First().Message.ShouldBe("Name is required");
    }

    [Fact]
    public void Validation_IsNot_FailsWhenConditionIsTrue()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("no-minors")
            .Assert<int>(p => p.Age)
            .IsNot((p, v) => v < 0)
            .Otherwise("Age cannot be negative");

        var person = new Person { Name = "Test", Age = -1 };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeTrue();
    }


    // ========== Multiple assertions ==========

    [Fact]
    public void Validation_MultipleAssertions_AllChecked()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("person-check")
            .Assert(p => p.Name)
            .Is((p, v) => !string.IsNullOrWhiteSpace(v))
            .Otherwise("Name is required");

        ruleSet.AddValidation<Person>("age-check")
            .Assert(p => p.Age)
            .Is((p, v) => v >= 0)
            .Otherwise("Age must be non-negative");

        var person = new Person { Name = "", Age = -1 };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeTrue();
        result.Events.Count.ShouldBe(2);
    }


    // ========== TryValidate extension ==========

    [Fact]
    public void TryValidate_Valid_ReturnsTrue()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("name-check")
            .Assert<string>(p => p.Name)
            .Is((p, v) => !string.IsNullOrWhiteSpace(v))
            .Otherwise("Name is required");

        var person = new Person { Name = "Alice" };
        var valid = ruleSet.TryValidate(person, out var violations);

        valid.ShouldBeTrue();
        violations.ShouldBeEmpty();
    }

    [Fact]
    public void TryValidate_Invalid_ReturnsFalse_WithViolations()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("name-check")
            .Assert<string>(p => p.Name)
            .Is((p, v) => !string.IsNullOrWhiteSpace(v))
            .Otherwise("Name is required");

        var person = new Person { Name = "" };
        var valid = ruleSet.TryValidate(person, out var violations);

        valid.ShouldBeFalse();
        violations.Count.ShouldBe(1);
        violations[0].Message.ShouldBe("Name is required");
    }


    // ========== Validation with group ==========

    [Fact]
    public void Validation_GroupedViolation_SetsGroup()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("email-check")
            .Assert<string>(p => p.Email)
            .Is((p, v) => !string.IsNullOrWhiteSpace(v))
            .Otherwise("contact", "Email is required");

        var person = new Person { Name = "Test", Email = "" };
        var result = EvaluateSafe(ruleSet, person);

        result.Events.First().Group.ShouldBe("contact");
    }


    // ========== String validators ==========

    [Fact]
    public void StringValidator_Required_Empty_Fails()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("name-required")
            .Assert<string>(p => p.Name)
            .Required();

        var person = new Person { Name = "" };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeTrue();
    }

    [Fact]
    public void StringValidator_Required_NonEmpty_Passes()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("name-required")
            .Assert<string>(p => p.Name)
            .Required();

        var person = new Person { Name = "Alice" };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeFalse();
    }

    [Fact]
    public void StringValidator_HasMinimumLength_TooShort_Fails()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("min-length")
            .Assert<string>(p => p.Name)
            .HasMinimumLength(3);

        var person = new Person { Name = "AB" };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeTrue();
    }

    [Fact]
    public void StringValidator_HasMinimumLength_LongEnough_Passes()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("min-length")
            .Assert<string>(p => p.Name)
            .HasMinimumLength(3);

        var person = new Person { Name = "Alice" };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeFalse();
    }

    [Fact]
    public void StringValidator_HasMaximumLength_TooLong_Fails()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("max-length")
            .Assert<string>(p => p.Name)
            .HasMaximumLength(3);

        var person = new Person { Name = "Alice" };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeTrue();
    }

    [Fact]
    public void StringValidator_IsIn_MatchingValue_Passes()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("status-check")
            .Assert<string>(p => p.Status)
            .IsIn("Active", "Inactive");

        var person = new Person { Name = "Test", Status = "Active" };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeFalse();
    }

    [Fact]
    public void StringValidator_IsIn_NonMatchingValue_Fails()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("status-check")
            .Assert<string>(p => p.Status)
            .IsIn("Active", "Inactive");

        var person = new Person { Name = "Test", Status = "Deleted" };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeTrue();
    }

    [Fact]
    public void StringValidator_IsMatch_ValidPattern_Passes()
    {
        var ruleSet = new RuleSet();

        // Use a regex without curly braces to avoid String.Format issue in the
        // validator's Otherwise message (curly braces are interpreted as format placeholders)
        ruleSet.AddValidation<Person>("name-pattern")
            .Assert<string>(p => p.Name)
            .IsMatch(@"^[A-Z][a-z]+$");

        var person = new Person { Name = "Alice", State = "TX" };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeFalse();
    }

    [Fact]
    public void StringValidator_IsMatch_InvalidPattern_Fails()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("name-pattern")
            .Assert<string>(p => p.Name)
            .IsMatch(@"^[A-Z][a-z]+$");

        var person = new Person { Name = "123" };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeTrue();
    }

    [Fact]
    public void StringValidator_IsMatch_RegexWithCurlyBraces_Passes()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("zip-pattern")
            .Assert<string>(p => p.Status)
            .IsMatch(@"^\d{5}$");

        var person = new Person { Name = "Test", Status = "12345" };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeFalse();
    }

    [Fact]
    public void StringValidator_IsMatch_RegexWithCurlyBraces_Fails()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("zip-pattern")
            .Assert<string>(p => p.Status)
            .IsMatch(@"^\d{5}$");

        var person = new Person { Name = "Test", Status = "ABC" };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeTrue();
    }


    // ========== Numeric validators ==========

    [Fact]
    public void IntValidator_Required_Zero_Fails()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("age-required")
            .Assert<int>(p => p.Age)
            .Required();

        var person = new Person { Name = "Test", Age = 0 };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeTrue();
    }

    [Fact]
    public void IntValidator_IsBetween_InRange_Passes()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("age-range")
            .Assert<int>(p => p.Age)
            .IsBetween(18, 65);

        var person = new Person { Name = "Test", Age = 30 };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeFalse();
    }

    [Fact]
    public void IntValidator_IsBetween_OutOfRange_Fails()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("age-range")
            .Assert<int>(p => p.Age)
            .IsBetween(18, 65);

        var person = new Person { Name = "Test", Age = 10 };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeTrue();
    }

    [Fact]
    public void DecimalValidator_IsGreaterThan_Above_Passes()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("salary-min")
            .Assert<decimal>(p => p.Salary)
            .IsGreaterThan(0m);

        var person = new Person { Name = "Test", Salary = 50000m };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeFalse();
    }


    // ========== Bool validators ==========

    [Fact]
    public void BoolValidator_IsTrue_True_Passes()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("active-check")
            .Assert<bool>(p => p.IsActive)
            .IsTrue();

        var person = new Person { Name = "Test", IsActive = true };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeFalse();
    }

    [Fact]
    public void BoolValidator_IsTrue_False_Fails()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("active-check")
            .Assert<bool>(p => p.IsActive)
            .IsTrue();

        var person = new Person { Name = "Test", IsActive = false };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeTrue();
    }

    [Fact]
    public void BoolValidator_IsFalse_False_Passes()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("inactive-check")
            .Assert<bool>(p => p.IsActive)
            .IsFalse();

        var person = new Person { Name = "Test", IsActive = false };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeFalse();
    }


    // ========== DateTime validators ==========

    [Fact]
    public void DateTimeValidator_Required_MinValue_Fails()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("dob-required")
            .Assert<DateTime>(p => p.BirthDate)
            .Required();

        var person = new Person { Name = "Test", BirthDate = DateTime.MinValue };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeTrue();
    }

    [Fact]
    public void DateTimeValidator_Required_ValidDate_Passes()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("dob-required")
            .Assert<DateTime>(p => p.BirthDate)
            .Required();

        var person = new Person { Name = "Test", BirthDate = new DateTime(1990, 1, 1) };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeFalse();
    }

    [Fact]
    public void DateTimeValidator_IsBetween_InRange_Passes()
    {
        var ruleSet = new RuleSet();

        ruleSet.AddValidation<Person>("dob-range")
            .Assert<DateTime>(p => p.BirthDate)
            .IsBetween(new DateTime(1950, 1, 1), new DateTime(2010, 12, 31));

        var person = new Person { Name = "Test", BirthDate = new DateTime(1990, 6, 15) };
        var result = EvaluateSafe(ruleSet, person);

        result.HasViolations.ShouldBeFalse();
    }


    // ========== Validation with When predicate ==========

    [Fact]
    public void Validation_When_PredicateTrue_ValidatesNormally()
    {
        var ruleSet = new RuleSet();

        var vr = ruleSet.AddValidation<Person>("conditional");
        vr.When(p => p.IsActive)
            .Assert<string>(p => p.Email)
            .Is((p, v) => !string.IsNullOrWhiteSpace(v))
            .Otherwise("Active persons must have email");

        var activePerson = new Person { Name = "Test", IsActive = true, Email = "" };
        var result = EvaluateSafe(ruleSet, activePerson);

        result.HasViolations.ShouldBeTrue();
    }

    [Fact]
    public void Validation_When_PredicateFalse_SkipsValidation()
    {
        var ruleSet = new RuleSet();

        var vr = ruleSet.AddValidation<Person>("conditional");
        vr.When(p => p.IsActive)
            .Assert<string>(p => p.Email)
            .Is((p, v) => !string.IsNullOrWhiteSpace(v))
            .Otherwise("Active persons must have email");

        var inactivePerson = new Person { Name = "Test", IsActive = false, Email = "" };
        var result = EvaluateSafe(ruleSet, inactivePerson);

        result.HasViolations.ShouldBeFalse();
    }

}
