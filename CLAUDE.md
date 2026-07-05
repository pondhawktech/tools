# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the entire solution
dotnet build pondhawk-tools.slnx

# Build a specific project
dotnet build src/Pondhawk.Core/Pondhawk.Core.csproj
dotnet build src/Pondhawk.Watch/Pondhawk.Watch.csproj
dotnet build src/Pondhawk.Rules/Pondhawk.Rules.csproj
dotnet build src/Pondhawk.Rql/Pondhawk.Rql.csproj
dotnet build src/Pondhawk.Hosting/Pondhawk.Hosting.csproj
dotnet build src/Pondhawk.Rules.EFCore/Pondhawk.Rules.EFCore.csproj
```

## Project Setup

- **.NET 10** targeting `net10.0` (SDK 10.0.103)
- **Central package management** via `Directory.Packages.props`
- **Nullable reference types** enabled in Core, Watch, and Hosting projects
## Architecture

This repository contains class libraries under `src/` that form the **Pondhawk** toolkit by Pond Hawk Technologies:

### Pondhawk.Core — Shared Foundation

Core utilities, pipeline infrastructure, mediator, and common exception types. Key subsystems:

- **Mediator** (`Pondhawk.Mediator`): Lightweight mediator pattern implementation for CQRS-style request/response dispatch with pipeline behaviors.
  - `IMediator` / `Mediator` — routes requests through pipeline behaviors to handlers. Uses cached handler wrappers to avoid reflection on every request. `SendAsync` returns a `Response<TResponse>` envelope (it is the single seam that converts a thrown error into a structured failure). The mediator is request/response only — exactly one handler per request. There is deliberately no notification/publish (one-to-many broadcast) channel; add one only when a real need for domain events arises, designing fan-out and error-aggregation semantics explicitly at that point.
  - `Response<T>` — success/failure envelope (`Ok`/`Value`/`Error`, plus `IsError`). The mediator maps a thrown `ExternalException` to `Response.Failure` with an `ErrorInfo` (preserving `ErrorKind`); unexpected exceptions become `ErrorKind.System` and are logged at `Error`; `OperationCanceledException` and missing-handler configuration errors still throw. Handlers/behaviors are unchanged — they keep returning `Task<TResponse>` and throwing. Ergonomic accessors for the app-side CRUD framework: `AsEntity` (success value, alias of `GetValueOrThrow`), `AsError` (the `ErrorInfo` to propagate; throws on success), and an implicit `ErrorInfo → Response<T>` conversion. Together these enable cross-type error propagation — `return source.AsError;` re-raises a failure from a `Response<X>` into a differently-typed `Response<T>` carrying the same `ErrorInfo`. There is deliberately no non-generic `Response`. Because `Response<T>` is a value type, `default(Response<T>)` (an uninitialized field, an unpopulated array slot, a dictionary miss) reads as a coherent `ErrorKind.System` failure rather than a success carrying a null value — the `Error` accessor coalesces a null-backed failure to a synthetic "not initialized" error, so the "error non-null iff not Ok" invariant holds on read and `Match`/`AsError`/`GetValueOrThrow` never NRE.
  - `Receipt` — the payload a mutation returns when it has no entity to hand back (a delete/bulk command uses `Response<Receipt>`). Carries `Affected` (entities affected: 1 for a single command, the real count for bulk, 0 = no-op). Factories: `Receipt.One` and `Receipt.Of(int)`.
  - `ErrorInfo` (`Pondhawk.Exceptions`) — transport-agnostic error shape shared by exceptions and the envelope. `ErrorKindPolicy.IsTransient(ErrorKind)` is the canonical retry/dead-letter default (`System`/`Remote`/`Concurrency`). `NotFoundException`/`ConflictException`/`NotAuthorizedException` are common `FluentException<T>` subclasses fixing their `Kind`. Note: the HTTP `ErrorKind → status` mapping deliberately lives in the ASP.NET layer, not Core.
  - `IRequest<TResponse>` — unified marker interface. `ICommand<TResponse>` and `IQuery<TResponse>` are convenience aliases preserving semantic intent.
  - `IRequestHandler<TRequest, TResponse>` — handler interface. `ICommandHandler` and `IQueryHandler` are convenience aliases.
  - `IPipelineBehavior<TRequest, TResponse>` — cross-cutting concerns (logging, validation) via delegate chain wrapping.
  - `ServiceCollectionExtensions.AddMediator(assemblies)` — registers mediator + auto-discovers handlers from assemblies. A single class that implements `IRequestHandler<,>` more than once is registered for **every** request type it handles, not just the first discovered. Discovery is resilient to unloadable types: a `ReflectionTypeLoadException` from an assembly (a missing optional dependency, a version mismatch) is caught, the loadable types are salvaged, and the skipped ones are reported via a `Warning` — a broken type no longer aborts startup, but such a handler is silently skipped and only surfaces as "No handler registered" at send time (the warning is the breadcrumb). An `AddMediator(ILoggerFactory?, assemblies)` overload supplies the factory for that warning (falls back to `NullLoggerFactory` when null). `AddPipelineBehavior(type)` registers open-generic behaviors.
  - `BatchExecutionContext` — `AsyncLocal`-based batch state tracking (nesting depth, batch ID). `BeginBatch(id)` returns disposable scope.
  - `BatchCommandResult` — type-erased result wrapper for heterogeneous batch command responses. Failures carry an `ErrorInfo` (with `ErrorKind`) via `Failed(commandType, entityUid, ErrorInfo)`; `ErrorMessage` is derived from `ErrorInfo.Explanation`, so the error kind survives a batch.
- **Configuration** (`Pondhawk.Configuration`): Configuration-driven DI module pattern.
  - `IServiceModule` — interface for modules that bundle related service registrations. Properties are populated via `IConfiguration` model binding, then `Build()` registers services.
  - `ServiceModuleExtensions.AddServiceModule<TModule>(configuration)` — binds a module from config and calls `Build()`. Overload accepts `Action<TModule>` for post-binding overrides.
- **Utilities**: Pipeline infrastructure (`IServiceCollection`-based DI registration), type extensions, date/time helpers.
- **Exceptions**: Common exception types.

### Pondhawk.Watch — Serilog Sink + Logging API (standalone)

A Serilog `ILogEventSink` with Channel-based batching, plus the full Watch logging API. Fully standalone — no dependency on Pondhawk.Core. Targets `net10.0` (single target — no conditional compilation).

- **Logging API** (`Pondhawk.Watch` namespace): `SerilogExtensions` provides extensions on `Serilog.ILogger`. Obtain a logger the standard Serilog way (e.g. `Log.ForContext<T>()`, which sets `SourceContext` to the type name), then call these on it:
  - **`ILogger.EnterMethod()`** — disposable method tracing scope with automatic entry/exit logging and elapsed time
  - **`ILogger.Inspect(name, value)`** — logs a name/value pair as `"{Name} = {Value}"` at Debug level
  - **`ILogger.LogObject(value)`** — serializes an object to JSON payload
  - **`ILogger.LogJson/LogSql/LogXml/LogYaml/LogText(title, content)`** — typed payload logging with syntax highlighting hints
  - Also: `WatchSwitchConfig`, `WatchPropertyNames`, serializers (`JsonObjectSerializer`), `PayloadType` enum, `[Sensitive]` attribute, `CorrelationManager`.
- **WatchSink**: `ILogEventSink` implementation with unbounded Channel batching. Converts Serilog events to Watch `LogEvent` instances with switch-based filtering. Circuit breaker for HTTP resilience.
- **WatchSinkExtensions**: Serilog configuration extensions. **`UseWatch(serverUrl, domain)`** is the primary API — sets `MinimumLevel.Verbose()` and adds the Watch sink so the Watch Server controls filtering via switches. `WriteTo.Watch()` is the lower-level alternative for manual minimum-level control.
- **Switching**: Dynamic log level control via `SwitchSource`/`SwitchDef` with pattern matching. `WatchSwitchSource` polls a Watch Server for switch configuration.
- **LogEvent/LogEventBatch**: Event model serialized as MemoryPack+Brotli for the wire; System.Text.Json (source-generated via `LogEventBatchContext`) is available for debugging/testing.

### Pondhawk.Rules — Rule Engine (standalone, no Core dependency)

A forward-chaining rule engine with type-based fact matching. Fully standalone — no dependency on Pondhawk.Core. Uses `Microsoft.Extensions.Logging` for listener infrastructure (Serilog picks up MS Logging events transparently). Key subsystems:

- **Builder** (`Pondhawk.Rules.Builder`): Fluent API for defining rules. `RuleBuilder<TFact1..TFact4>` creates `Rule<T>` instances via `When().And().Then()` chains. Supports up to 4 fact types per rule. Multi-fact rules have per-fact `When(Func<T, bool>)`/`And(Func<T, bool>)` overloads that enable C# pattern matching with full type inference. Rules have salience (priority), mutex (mutual exclusion), fire-once, inception/expiration.
- **Evaluation** (`Pondhawk.Rules.Evaluation`): `EvaluationPlan` generates all fact-type combinations using variations-with-repetition. `TupleEvaluator` executes rules in salience order against fact tuples. `FactSpace` stores facts with int selectors for memory efficiency.
- **Tree** (`Pondhawk.Rules.Tree`): `RuleTree` indexes rules by fact types for fast lookup with polymorphic type matching.
- **Validation** (`Pondhawk.Rules.Validators`): `ValidationBuilder<TFact>` with `Assert<T>(expr).Is().IsNot().Otherwise()` chains. Runs at very high salience.
- **Factory** (`Pondhawk.Rules.Factory`): `RuleSet` for runtime rule creation without predefined builder classes. `RuleSetFactory` uses `Lazy<T>` for thread-safe exactly-once initialization.
- **Listeners** (`Pondhawk.Rules.Listeners`): Observer pattern (`IEvaluationListener`) for tracing rule evaluation. `WatchEvaluationListener` uses `Microsoft.Extensions.Logging.ILogger`. `WatchEvaluationListenerFactory` uses `ILoggerFactory` (defaults to `NullLoggerFactory`).
- **RuleEvent**: Sealed event type with `IEquatable<RuleEvent>`, init-only setters, nested `EventCategory` enum (`Info`, `Warning`, `Violation`). Replaces the former Core `EventDetail` dependency.
- **Exceptions**: `ViolationsExistException`, `NoRulesEvaluatedException`, `EvaluationExhaustedException` — all extend `Exception` directly.

Evaluation flow: `RuleBuilder` → `RuleTree` (indexed by type) → `EvaluationPlan` (generates steps) → `TupleEvaluator` (executes) → `EvaluationResults` (aggregates scores/events/violations). Forward chaining via `InsertFact`/`ModifyFact`/`RetractFact` triggers re-evaluation.

**Authoring guidelines**: Avoid `||` in conditions — each OR branch should be a separate rule for atomicity and traceability. Prefer `.And()` chains over `&&` inside a single predicate.

### Pondhawk.Rules.EFCore — EF Core SaveChanges Validation

Pre-save entity validation interceptor that hooks into EF Core's `SaveChangesInterceptor`. Validates all `Added` and `Modified` entities through `Pondhawk.Rules` before they reach the database.

- **RuleValidationInterceptor**: `SaveChangesInterceptor` subclass that pulls entities from `ChangeTracker`, calls `IRuleSet.Validate()`, and throws `EntityValidationException` if validation fails. Overrides both `SavingChanges` and `SavingChangesAsync`.
- **EntityValidationException**: Carries `ValidationResult` with structured violations. Formats a human-readable message from violations.
- **DbContextOptionsBuilderExtensions**: `AddRuleValidation(IRuleSet)` convenience method.
- Minimum EF Core version: 5.0.0.

### Pondhawk.Rql — Resource Query Language (standalone, no Core dependency)

A filtering DSL with AST, fluent builder, parser, and multiple serialization targets. Fully standalone — no dependency on Pondhawk.Core.

- **AST**: `RqlTree` (root) contains `Criteria` (list of `IRqlPredicate`). `RqlOperator` enum: Equals, NotEquals, LesserThan, GreaterThan, Between, In, NotIn, StartsWith, Contains, etc.
- **Builder** (`Pondhawk.Rql.Builder`): `RqlFilterBuilder<TTarget>` provides fluent API: `.Where(expr).Equals(value).And(expr).GreaterThan(value)`. `Introspect()` builds filters from objects decorated with `[CriterionAttribute]`.
- **Parser** (`Pondhawk.Rql.Parser`): Parses RQL criteria text back into `RqlTree` AST using the **Sprache** parser combinator library. `RqlLanguageParser.ToCriteria(string)` parses criteria. Value type prefixes: `@` for DateTime, `#` for decimal, `'...'` for strings.
- **Serialization** (`Pondhawk.Rql.Serialization`): Three output formats:
  - `ToRql()` — RQL text: `(eq(Name,'John'),gt(Age,30))`
  - `ToLambda<T>()` / `ToExpression<T>()` — compiled LINQ expressions
  - `ToSqlQuery()` / `ToSqlWhere()` — parameterized SQL
  - `ToDescription()` — human-readable English: `"Name equals 'John' and Age is greater than 30"`

### Pondhawk.Hosting — Service Startup Extensions for Generic Host

Lightweight service lifecycle management for `Microsoft.Extensions.Hosting`. Standalone — no dependency on any other Pondhawk project. Only depends on `Microsoft.Extensions.Hosting.Abstractions`.

- **`AddSingletonWithStart<TService>(startAction)`** — registers a singleton and a start lambda, co-located at the registration site. Supports sync, async, and optional stop lambdas:
  ```csharp
  services.AddSingletonWithStart<RuleSetFactory>(f => f.Start());
  services.AddSingletonWithStart<RuleSetFactory>(f => f.Start(), f => f.Stop());
  services.AddSingletonWithStart<MyService>((svc, ct) => svc.InitAsync(ct), (svc, ct) => svc.StopAsync(ct));
  ```
- **ServiceStarterHostedService**: `IHostedService` that resolves all registered descriptors and calls start lambdas on host startup, stop lambdas in reverse order on shutdown. Auto-registered via `TryAddEnumerable` — only one instance regardless of how many services are registered. Logs each service start/stop via `[LoggerMessage]` source-generated methods.
- **AppLifecycleService**: `IHostedService` providing file-based lifecycle signaling against a flag directory (defaults to `AppContext.BaseDirectory`). On start it clears stale flags, then writes `started.flag` / `stopped.flag` from the `IHostApplicationLifetime` callbacks, and uses a `FileSystemWatcher` to watch for an externally-created `muststop.flag` — which triggers a graceful `StopApplication()`. Useful for out-of-band shutdown signaling (e.g. orchestrators dropping a flag file). Logs via a source-generated `[LoggerMessage]`.
- Falls back to `NullLoggerFactory` when `AddLogging()` hasn't been called (e.g. in test scenarios).

### Dependency Graph

```
Pondhawk.Core     (foundation — mediator, pipeline infrastructure, utilities, exceptions)
Pondhawk.Watch    (standalone — logging API, Serilog sink, net10.0)
Pondhawk.Rules    (standalone)
Pondhawk.Rules.EFCore ──→ Pondhawk.Rules
Pondhawk.Rql      (standalone)
Pondhawk.Hosting  (standalone)
```

## Related Repository

**[pondhawk/watch-server](https://github.com/pondhawk/watch-server)** (`E:\repository\watch-server`) — The Pondhawk Watch Server, a log event aggregation server with ASP.NET Core Web API, SQLite storage, React UI, and Winston transport for Node.js. Consumes the `Pondhawk.Watch` NuGet package from this repo as its client-side logging sink.

## Conventions

- Namespaces match project/folder structure: `Pondhawk.Mediator`, `Pondhawk.Configuration`, `Pondhawk.Rules`, `Pondhawk.Rules.Builder`, `Pondhawk.Rules.Evaluation`, `Pondhawk.Rql`, `Pondhawk.Rql.Builder`, `Pondhawk.Rql.Parser`, `Pondhawk.Rql.Serialization`, `Pondhawk.Hosting`
- Exception: `Pondhawk.Core` project uses `RootNamespace=Pondhawk`
- `LangVersion` varies: `default` in Rules and Hosting, `latestmajor` in Rql, `latest` in Watch
