using System.Linq.Expressions;
using Pondhawk.Rules.Builder;
using Pondhawk.Rules.Util;
using Pondhawk.Rules.Validators;

namespace Pondhawk.Rules;

/// <summary>
/// Base class for defining validation rules using a fluent <c>Assert</c>/<c>AssertOver</c> API with support for mutex and conditional groups.
/// </summary>
/// <remarks>
/// Validations run at very high salience (100000+) so they always fire before business rules.
/// Use <c>Assert&lt;T&gt;(expr)</c> for scalar properties and <c>AssertOver&lt;T&gt;(expr)</c> for collection properties.
/// Use <c>When(predicate, () =&gt; { ... })</c> to group validations under a condition.
/// Use <c>Mutex(() =&gt; { ... })</c> so only the first failing validation in a group fires.
/// Violations are collected as <see cref="RuleEvent"/> with <c>EventCategory.Violation</c>.
/// </remarks>
/// <example>
/// <code>
/// public class PersonValidation : ValidationBuilder&lt;Person&gt;
/// {
///     public PersonValidation()
///     {
///         Assert&lt;string&gt;(p =&gt; p.Name).Required();
///         Assert&lt;string&gt;(p =&gt; p.Email).Required().IsEmail();
///         Assert&lt;int&gt;(p =&gt; p.Age).IsGreaterThanOrEqual(0).IsLessThanOrEqual(150);
///
///         When(p =&gt; p.IsEmployee, () =&gt;
///         {
///             Assert&lt;string&gt;(p =&gt; p.EmployeeId).Required();
///         });
///     }
/// }
/// </code>
/// </example>
public abstract class ValidationBuilder<TFact> : AbstractRuleBuilder, IBuilder
{

    private int _currentSalience = 100000;
    private string _currentMutex = "";

    /// <summary>
    /// Creates a validation assertion for a scalar property extracted from the fact.
    /// </summary>
    /// <typeparam name="TType">The type of the property being validated.</typeparam>
    /// <param name="extractor">An expression that extracts the property value from the fact.</param>
    /// <returns>A validator for defining conditions on the extracted property.</returns>
    public virtual IValidator<TFact, TType> Assert<TType>(Expression<Func<TFact, TType>> extractor)
    {

        var nameSpace = GetType().Namespace;
        var fullSetName = $"{nameSpace}.{SetName}";

        var factName = typeof(TFact).GetConciseName();
        var ruleName = (extractor.Body is MemberExpression body ? $"{factName}.{body.Member.Name}" : factName);

        var rule = new ValidationRule<TFact>(fullSetName, ruleName);


        // Apply default salience
        rule.WithSalience(_currentSalience);

        // Apply default inception and expiration
        rule.WithInception(DefaultInception);
        rule.WithExpiration(DefaultExpiration);


        if (_currentPredicate is not null)
            rule.When(_currentPredicate);

        if (!string.IsNullOrWhiteSpace(_currentMutex))
            rule.InMutex(_currentMutex);


        Sinks.Add(t => t.Add(typeof(TFact), rule));

        _currentSalience += 100;

        var validator = rule.Assert(extractor);

        return validator;

    }


    /// <summary>
    /// Creates a standalone validation rule for advanced scenarios such as cascade or custom evaluation.
    /// </summary>
    /// <returns>A new validation rule for further configuration.</returns>
    protected ValidationRule<TFact> Rule()
    {

        var nameSpace = GetType().Namespace;
        var fullSetName = $"{nameSpace}.{SetName}";

        var factName = typeof(TFact).GetConciseName();
        var ruleName = factName;

        var rule = new ValidationRule<TFact>(fullSetName, ruleName);


        rule.WithSalience(_currentSalience);

        // Apply default inception and expiration
        rule.WithInception(DefaultInception);
        rule.WithExpiration(DefaultExpiration);


        if (_currentPredicate is not null)
            rule.When(_currentPredicate);


        if (!string.IsNullOrWhiteSpace(_currentMutex))
            rule.InMutex(_currentMutex);


        Sinks.Add(t => t.Add(typeof(TFact), rule));

        _currentSalience += 100;

        return rule;

    }


    /// <summary>
    /// Groups the validations defined in the builder action under an auto-generated mutex so only the first failing rule fires.
    /// </summary>
    /// <param name="builder">An action that defines the validation rules within the mutex group.</param>
    protected void Mutex(Action builder)
    {

        try
        {
            _currentMutex = Ulid.NewUlid().ToString(null, System.Globalization.CultureInfo.InvariantCulture);
            builder();
        }
        finally
        {
            _currentMutex = "";
        }

    }


    /// <summary>
    /// Groups the validations defined in the builder action under a named mutex so only the first failing rule fires.
    /// </summary>
    /// <param name="name">The mutex group name.</param>
    /// <param name="builder">An action that defines the validation rules within the mutex group.</param>
    protected void Mutex(string name, Action builder)
    {

        try
        {
            _currentMutex = name;
            builder();
        }
        finally
        {
            _currentMutex = "";
        }

    }


    private Func<TFact, bool> _currentPredicate;

    /// <summary>
    /// Conditionally applies the validations defined in the builder action only when the predicate is true.
    /// </summary>
    /// <param name="predicate">A function that returns <c>true</c> if the contained validations should apply.</param>
    /// <param name="builder">An action that defines the conditional validation rules.</param>
    protected void When(Func<TFact, bool> predicate, Action builder)
    {

        try
        {
            _currentPredicate = predicate;
            builder();
        }
        finally
        {
            _currentPredicate = null;
        }

    }


    /// <summary>
    /// Conditionally applies the validations defined in the builder action only when the predicate is false.
    /// </summary>
    /// <param name="predicate">A function whose negation gates the contained validations.</param>
    /// <param name="builder">An action that defines the conditional validation rules.</param>
    protected void Unless(Func<TFact, bool> predicate, Action builder)
    {
        When(f => !predicate(f), builder);
    }


    /// <summary>
    /// Creates a validation assertion for an enumerable property extracted from the fact, enabling collection-level validation.
    /// </summary>
    /// <typeparam name="TType">The element type of the enumerable property.</typeparam>
    /// <param name="extractor">An expression that extracts the enumerable from the fact.</param>
    /// <returns>An enumerable validator for defining conditions on the extracted collection.</returns>
    public virtual EnumerableValidator<TFact, TType> AssertOver<TType>(Expression<Func<TFact, IEnumerable<TType>>> extractor)
    {

        var nameSpace = GetType().Namespace;
        var fullSetName = $"{nameSpace}.{SetName}";

        var factName = typeof(TFact).GetConciseName();
        var ruleName = (extractor.Body is MemberExpression body ? $"{factName}.{body.Member.Name}" : factName);

        var rule = new ValidationRule<TFact>(fullSetName, ruleName);

        rule.WithSalience(_currentSalience);
        rule.WithInception(DefaultInception);
        rule.WithExpiration(DefaultExpiration);

        if (_currentPredicate is not null)
            rule.When(_currentPredicate);

        if (!string.IsNullOrWhiteSpace(_currentMutex))
            rule.InMutex(_currentMutex);

        Sinks.Add(t => t.Add(typeof(TFact), rule));

        _currentSalience += 100;

        return rule.AssertOver(extractor);

    }


}
