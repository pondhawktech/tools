using Pondhawk.Exceptions;
using Pondhawk.Mediator;
using Pondhawk.Rules;

namespace Pondhawk.Api.Behaviors;

/// <summary>
/// Mediator pipeline behavior that validates each request against the registered
/// <see cref="IRuleSet"/>s (Pondhawk.Rules). Any violations become a
/// <see cref="FailedValidationException"/>, which the mediator envelopes as
/// <see cref="ErrorKind.Predicate"/> — rendered by the response filter as <c>422</c> with the
/// violations in the problem detail.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IRuleSet> ruleSets)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        var violations = new List<EventDetail>();

        foreach (var rules in ruleSets)
        {
            if (!rules.TryValidate(request, out var events))
                violations.AddRange(events.Select(ToDetail));
        }

        if (violations.Count > 0)
            throw new FailedValidationException(violations);

        return await next().ConfigureAwait(false);
    }

    private static EventDetail ToDetail(RuleEvent violation) =>
        EventDetail.Build()
            .WithCategory(EventDetail.EventCategory.Violation)
            .WithRuleName(violation.RuleName)
            .WithGroup(violation.Group)
            .WithExplanation(violation.Message);
}
