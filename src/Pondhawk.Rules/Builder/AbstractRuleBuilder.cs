/*
The MIT License (MIT)

Copyright (c) 2017 The Kampilan Group Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using Pondhawk.Rules.Evaluation;
using Pondhawk.Rules.Tree;

namespace Pondhawk.Rules.Builder;

/// <summary>
/// Base class for rule builders, providing fact-space operations (Insert, Modify, Retract), lookup tables, scoring, and event helpers.
/// </summary>
public abstract class AbstractRuleBuilder
{

    /// <summary>Initializes a new instance of the <see cref="AbstractRuleBuilder"/> class with the set name derived from the concrete type name.</summary>
    protected AbstractRuleBuilder()
    {
        SetName = GetType().Name;
    }

    /// <summary>Gets or sets the name of the rule set this builder contributes to.</summary>
    public string SetName { get; protected set; }

    /// <summary>Gets or sets a value indicating whether rules created by this builder fire only once by default.</summary>
    public bool DefaultFireOnce { get; protected set; }

    /// <summary>Gets or sets the default salience applied to rules created by this builder.</summary>
    public int DefaultSalience { get; protected set; } = 500;

    /// <summary>Gets or sets the default inception date applied to rules created by this builder.</summary>
    public DateTime DefaultInception { get; protected set; } = DateTime.MinValue;

    /// <summary>Gets or sets the default expiration date applied to rules created by this builder.</summary>
    public DateTime DefaultExpiration { get; protected set; } = DateTime.MaxValue;


    /// <summary>Gets or sets the target fact types for rules in this builder.</summary>
    protected Type[] Targets { get; set; } = [];

    /// <summary>Gets the collection of rules created by this builder.</summary>
    public ISet<IRule> Rules { get; } = new HashSet<IRule>();

    /// <summary>Gets the collection of sink actions used for multi-type rule registration.</summary>
    public ISet<Action<IRuleSink>> Sinks { get; } = new HashSet<Action<IRuleSink>>();

    /// <summary>
    /// Loads the rules and sink actions defined by this builder into the specified sink for eventual evaluation.
    /// </summary>
    /// <param name="ruleSink">The sink that receives the builder's rules.</param>
    public virtual void LoadRules(IRuleSink ruleSink)
    {

        foreach (var sink in Sinks)
            sink(ruleSink);

        if (Targets.Length > 0)
            ruleSink.Add(Targets, Rules);

    }


    /// <summary>Gets the name of the rule currently being fired in this evaluation session.</summary>
    protected static string CurrentRuleName => RuleThreadLocalStorage.CurrentContext.CurrentRuleName;

    /// <summary>Looks up a member from the default lookup table by key.</summary>
    /// <typeparam name="TMember">The type of the member to retrieve.</typeparam>
    /// <param name="key">The key to look up.</param>
    /// <returns>The member associated with the key.</returns>
    protected static TMember Lookup<TMember>(object key)
    {
        return RuleThreadLocalStorage.CurrentContext.Lookup<TMember>(key);
    }

    /// <summary>Looks up a member from a named lookup table by key.</summary>
    /// <typeparam name="TMember">The type of the member to retrieve.</typeparam>
    /// <param name="name">The name of the lookup table.</param>
    /// <param name="key">The key to look up.</param>
    /// <returns>The member associated with the key.</returns>
    protected static TMember Lookup<TMember>(string name, object key)
    {
        return RuleThreadLocalStorage.CurrentContext.Lookup<TMember>(name, key);
    }

    /// <summary>Gets the shared dictionary for passing data between rule consequences.</summary>
    protected static IDictionary<string, object> Shared => RuleThreadLocalStorage.CurrentContext.Shared;

    /// <summary>Emits an informational event with the specified group, template, and parameters.</summary>
    /// <param name="group">The event group.</param>
    /// <param name="template">The message template.</param>
    /// <param name="markers">The format parameters for the template.</param>
    protected static void Info(string group, string template, params object[] markers)
    {
        RuleThreadLocalStorage.CurrentContext.Event(RuleEvent.EventCategory.Info, group, template, markers);
    }

    /// <summary>Emits a violation event with the specified group, template, and parameters.</summary>
    /// <param name="group">The event group.</param>
    /// <param name="template">The message template.</param>
    /// <param name="markers">The format parameters for the template.</param>
    protected static void Violation(string group, string template, params object[] markers)
    {
        RuleThreadLocalStorage.CurrentContext.Event(RuleEvent.EventCategory.Violation, group, template, markers);
    }

    /// <summary>Adds the specified amount to the affirmation score.</summary>
    /// <param name="amount">The affirmation weight to add.</param>
    protected static void Affirm(int amount)
    {
        RuleThreadLocalStorage.CurrentContext.Results.Affirm(amount);
    }

    /// <summary>Adds the specified amount to the veto score.</summary>
    /// <param name="amount">The veto weight to add.</param>
    protected static void Veto(int amount)
    {
        RuleThreadLocalStorage.CurrentContext.Results.Veto(amount);
    }

    /// <summary>Sends a debug message to the evaluation listener.</summary>
    /// <param name="template">The message template.</param>
    /// <param name="markers">The format parameters for the template.</param>
    protected static void Debug(string template, params object[] markers)
    {
        RuleThreadLocalStorage.CurrentContext.Listener.Debug(template, markers);
    }

    /// <summary>Inserts a new fact into the fact space, triggering forward chaining.</summary>
    /// <param name="fact">The fact to insert.</param>
    protected static void Insert(object fact)
    {
        RuleThreadLocalStorage.CurrentContext.InsertFact(fact);
    }

    /// <summary>Signals that a fact has been modified, triggering re-evaluation.</summary>
    /// <param name="fact">The modified fact.</param>
    protected static void Modify(object fact)
    {
        RuleThreadLocalStorage.CurrentContext.ModifyFact(fact);
    }

    /// <summary>Retracts a fact from the fact space, triggering re-evaluation.</summary>
    /// <param name="fact">The fact to retract.</param>
    protected static void Retract(object fact)
    {
        RuleThreadLocalStorage.CurrentContext.RetractFact(fact);
    }
}

