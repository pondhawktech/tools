<p align="center">
  <img src="pht-small-logo.png" alt="Pondhawk Tools" width="120" />
</p>

<h1 align="center">Pondhawk Tools</h1>

<p align="center">
  A modular .NET toolkit for rule evaluation, resource querying, structured logging, and service lifecycle management.
</p>

<p align="center">
  <a href="https://github.com/pondhawktech/tools/actions/workflows/build.yml"><img src="https://github.com/pondhawktech/tools/actions/workflows/build.yml/badge.svg" alt="Build" /></a>
  <img src="https://img.shields.io/badge/.NET-10.0-512bd4" alt=".NET 10" />
  <img src="https://img.shields.io/badge/license-MIT-blue" alt="MIT License" />
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/Pondhawk.Core"><img src="https://img.shields.io/nuget/v/Pondhawk.Core?label=Core" alt="Pondhawk.Core" /></a>
  <a href="https://www.nuget.org/packages/Pondhawk.Logging"><img src="https://img.shields.io/nuget/v/Pondhawk.Logging?label=Logging" alt="Pondhawk.Logging" /></a>
  <a href="https://www.nuget.org/packages/Pondhawk.Logging.Watch"><img src="https://img.shields.io/nuget/v/Pondhawk.Logging.Watch?label=Logging.Watch" alt="Pondhawk.Logging.Watch" /></a>
  <a href="https://www.nuget.org/packages/Pondhawk.Rules"><img src="https://img.shields.io/nuget/v/Pondhawk.Rules?label=Rules" alt="Pondhawk.Rules" /></a>
  <a href="https://www.nuget.org/packages/Pondhawk.Rules.EFCore"><img src="https://img.shields.io/nuget/v/Pondhawk.Rules.EFCore?label=Rules.EFCore" alt="Pondhawk.Rules.EFCore" /></a>
  <a href="https://www.nuget.org/packages/Pondhawk.Hosting"><img src="https://img.shields.io/nuget/v/Pondhawk.Hosting?label=Hosting" alt="Pondhawk.Hosting" /></a>
  <a href="https://www.nuget.org/packages/Pondhawk.Api"><img src="https://img.shields.io/nuget/v/Pondhawk.Api?label=Api" alt="Pondhawk.Api" /></a>
</p>

---

## Overview

Pondhawk Tools is a collection of class libraries built by [Pond Hawk Technologies](https://github.com/pondhawk). Each package is independently versioned and can be adopted on its own.

| Package | Description |
|---------|-------------|
| [**Pondhawk.Rules**](src/Pondhawk.Rules/README.md) | Forward-chaining rule engine with type-based fact matching, scoring, and validation |
| [**Pondhawk.Rules.EFCore**](src/Pondhawk.Rules.EFCore/README.md) | EF Core `SaveChangesInterceptor` that validates entities through Rules before save |
| [**Pondhawk.Core**](src/Pondhawk.Core/README.md) | Shared foundation — mediator, configuration modules, pipeline infrastructure, utilities, exceptions |
| [**Pondhawk.Logging**](src/Pondhawk.Logging/README.md) | Serilog-based structured logging API (method tracing, object/payload logging, `[Sensitive]` masking) + the `ILoggerSource` acquisition abstraction |
| [**Pondhawk.Logging.Watch**](src/Pondhawk.Logging.Watch/README.md) | Watch Server provider for Pondhawk.Logging — Serilog sink with Channel-based batching, dynamic switching, and a switch-aware `ILoggerSource` |
| [**Pondhawk.Hosting**](src/Pondhawk.Hosting/README.md) | `AddSingletonWithStart<T>()` pattern for co-locating service registration with startup logic |
| [**Pondhawk.Api**](src/Pondhawk.Api/README.md) | ASP.NET Core web kit — endpoint modules, `Response<T>`→ProblemDetails filter, gateway identity, diagnostics middleware, and JSON conventions |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.103 or later)

### Installation

Stable releases are published to [nuget.org](https://www.nuget.org/profiles/pondhawk). Install the packages you need:

```bash
dotnet add package Pondhawk.Rules
dotnet add package Pondhawk.Logging
dotnet add package Pondhawk.Logging.Watch
dotnet add package Pondhawk.Api
```

Pre-release builds are available on [GitHub Packages](https://github.com/orgs/pondhawk/packages). Add the feed to your NuGet configuration:

```xml
<PackageSource>
  <add key="pondhawk" value="https://nuget.pkg.github.com/pondhawk/index.json" />
</PackageSource>
```

### Building from Source

```bash
dotnet build pondhawk-tools.slnx
dotnet test pondhawk-tools.slnx
```

---

## Pondhawk.Rules

A forward-chaining rule engine with fluent rule definition, multi-fact matching, validation, and decision scoring.

### Defining Rules

Rules are created with a `RuleSet` using the fluent `When()` / `And()` / `Then()` API:

```csharp
var rules = new RuleSet();

rules.AddRule<Order>("HighValueOrder")
    .When(o => o.Total > 1000)
    .Then("Pricing", "Order {Total} exceeds high-value threshold", o => o.Total);

rules.AddRule<Order>("DiscountEligible")
    .When(o => o.Total > 500)
    .And(o => o.Customer.IsLoyaltyMember)
    .Then(o => o.DiscountPct = 0.10m);

rules.Build();
```

### Multi-Fact Rules

Rules can match across 2, 3, or 4 fact types simultaneously:

```csharp
rules.AddRule<Order, Customer>("LoyaltyBonus")
    .When((order, customer) => order.Total > 200 && customer.Tier == "Gold")
    .Then((order, customer) => order.DiscountPct += 0.05m);
```

Per-fact overloads let you write conditions against a single fact type, which enables C# pattern matching with full type inference:

```csharp
rules.AddRule<Order, Customer>("GoldCustomer")
    .When((Func<Customer, bool>)(c => c.Tier is "Gold" or "Platinum"))
    .And((Func<Order, bool>)(o => o.Total is > 200 and < 10_000))
    .Then((order, customer) => order.DiscountPct += 0.05m);
```

### Validation

`ValidationRule<T>` provides property-level assertions with the `Assert<T>().Is().Otherwise()` pattern:

```csharp
rules.AddValidation<Customer>("CustomerValidation")
    .Assert<string>(c => c.Name)
        .IsNot((c, name) => string.IsNullOrWhiteSpace(name))
        .Otherwise("Customer name is required")
    .Assert<string>(c => c.Email)
        .Is((c, email) => email.Contains('@'))
        .Otherwise("Validation", "{Name} has an invalid email", c => c.Name);
```

Validation rules run at very high salience by default, ensuring they execute before business rules.

### Scoring

Use `ThenAffirm()` and `ThenVeto()` to build a weighted decision score:

```csharp
rules.AddRule<Application>("CreditCheck")
    .When(a => a.CreditScore > 700)
    .ThenAffirm(10)
    .OtherwiseVeto(20);

rules.AddRule<Application>("IncomeCheck")
    .When(a => a.AnnualIncome > 50_000)
    .ThenAffirm(15);

// After evaluation:
// results.Score > 0 means approved
```

### Forward Chaining

Rules can insert, modify, or retract facts to trigger re-evaluation. Use `Cascade()` to pull in related objects:

```csharp
rules.AddRule<Order>("ApplyLineItems")
    .When(o => o.LineItems.Any())
    .Then(o => { /* process order */ })
    .CascadeAll(o => o.LineItems);

rules.AddRule<LineItem>("FlagBackorder")
    .When(li => li.Quantity > li.InStock)
    .Then(li => li.IsBackordered = true)
    .Modifies(li => li);
```

### Rule Properties

Fine-tune rule behavior with salience (priority), mutex (mutual exclusion), fire-once, and time windows:

```csharp
rules.AddRule<Order>("PriorityRule")
    .WithSalience(900)                            // higher = runs first (default: 500)
    .InMutex("ShippingMethod")                    // only one rule wins per mutex group
    .FireOnce()                                   // skip after first match
    .WithInception(new DateTime(2025, 1, 1))      // active from
    .WithExpiration(new DateTime(2025, 12, 31))   // active until
    .When(o => o.IsExpedited)
    .Then(o => o.ShippingMethod = "Overnight");
```

### Evaluation

Build an `EvaluationContext`, add facts, and evaluate:

```csharp
var context = rules.GetEvaluationContext()
    .WithDescription("Order Processing")
    .WithMaxEvaluations(1000)
    .WithMaxDuration(5000);

context.AddFacts(order, customer);
var results = context.Evaluate();

// Inspect results
Console.WriteLine($"Score: {results.Score}");
Console.WriteLine($"Violations: {results.ViolationCount}");
Console.WriteLine($"Rules fired: {results.TotalFired}");

foreach (var violation in results.GetViolations())
    Console.WriteLine($"  [{violation.Group}] {violation.Message}");
```

Or use the convenience extensions:

```csharp
// Quick evaluation
var results = rules.Evaluate(order, customer);

// Validation with structured result
var validation = rules.Validate(order);
if (!validation.IsValid)
{
    foreach (var (group, violations) in validation.ViolationsByGroup)
        Console.WriteLine($"{group}: {violations.Count} violation(s)");
}
```

### RuleSetFactory

For production scenarios, `RuleSetFactory` provides lazy initialization and namespace-scoped rule sets:

```csharp
var factory = new RuleSetFactory();
factory.AddSources(new OrderRules(), new CustomerRules());
factory.Start();

IRuleSet ruleSet = factory.GetRuleSet();
IRuleSet orderOnly = factory.GetRuleSet("OrderRules");
```

### EF Core Integration

`Pondhawk.Rules.EFCore` validates all `Added` and `Modified` entities through your rule set before `SaveChanges`:

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
           .AddRuleValidation(ruleSet));
```

If validation fails, `EntityValidationException` is thrown with the full `ValidationResult`:

```csharp
try
{
    await dbContext.SaveChangesAsync();
}
catch (EntityValidationException ex)
{
    foreach (var v in ex.ValidationResult.Violations)
        Console.WriteLine($"  {v.Message}");
}
```

---

## Other Packages

### Pondhawk.Core

Shared foundation with mediator, configuration-driven DI modules, pipeline infrastructure, type utilities, and common exception types.

```csharp
// Mediator — CQRS-style request dispatch with pipeline behaviors
services.AddMediator(typeof(MyHandler).Assembly);
services.AddPipelineBehavior(typeof(LoggingBehavior<,>));

// SendAsync returns a Response<T> envelope: handlers throw, the mediator maps the throw to a
// structured failure (preserving ErrorKind) so queue/batch callers branch without catching.
Response<int> result = await mediator.SendAsync(new CreateOrderCommand { ... });

// Configuration — bind modules from IConfiguration and register services
services.AddServiceModule<DatabaseModule>(configuration);

// Pipeline — composable step-based execution
services.AddPipelineFactory();
services.AddPipeline<OrderContext>(steps => steps
    .Add<ValidateStep>()
    .Add<CalculateTaxStep>()
    .Add<SaveStep>());
```

### Pondhawk.Logging

Serilog-based structured logging API — method tracing, object/typed-payload logging, `[Sensitive]` masking — plus the `ILoggerSource` acquisition abstraction. No sink; provider packages build on it.

```csharp
using Pondhawk.Logging;
using Serilog;

public class OrderService
{
    private readonly ILogger _log;

    // Inject ILoggerSource; CreateLogger<T>() sets SourceContext to the type name.
    public OrderService(ILoggerSource loggers) => _log = loggers.CreateLogger<OrderService>();

    public void Process(int orderId, Order order, string sqlText)
    {
        using var _ = _log.EnterMethod();
        _log.Inspect("orderId", orderId);   // logs "orderId = 123" at Debug
        _log.LogObject("payload", order);   // serializes object to JSON
        _log.LogSql("query", sqlText);      // typed payload with syntax hint
    }
}
```

### Pondhawk.Logging.Watch

Watch Server provider for Pondhawk.Logging — a Serilog `ILogEventSink` with unbounded `Channel<T>` batching, circuit-breaker resilience, dynamic switch-based level control, and a switch-aware `ILoggerSource` (`WatchLoggerSource`) that skips payload serialization for switch-dropped categories.

```csharp
using Pondhawk.Logging;
using Pondhawk.Logging.Watch;
using Serilog;

// Watch Server controls log levels via switches; share the switch source with a WatchLoggerSource.
Log.Logger = new LoggerConfiguration()
    .UseWatch("http://localhost:11000", "MyApp", out var switchSource)
    .CreateLogger();

ILoggerSource loggers = new WatchLoggerSource(Log.Logger, switchSource);
```

### Pondhawk.Hosting

Co-locate service registration with startup/shutdown logic using `AddSingletonWithStart<T>()`. A single `IHostedService` resolves all registered services and calls their start/stop actions.

```csharp
services.AddSingletonWithStart<RuleSetFactory>(f => f.Start());
services.AddSingletonWithStart<CacheService>(
    (svc, ct) => svc.WarmUpAsync(ct),
    (svc, ct) => svc.FlushAsync(ct));
```

Also includes `AppLifecycleService`, an `IHostedService` that signals lifecycle state through flag files (`started.flag` / `stopped.flag`) and watches for an external `muststop.flag` to trigger a graceful shutdown.

### Pondhawk.Api

An ASP.NET Core web kit built on Pondhawk.Core, Pondhawk.Logging, and Pondhawk.Rules. Endpoints are grouped into `IEndpointModule`s that dispatch to the mediator; handlers return a `Response<T>` envelope and stay transport-agnostic, while `ResponseEndpointFilter` renders it to `Ok`/JSON on success or a ProblemDetails (`application/problem+json`) on failure — with the `ErrorKind` mapped to the right HTTP status.

```csharp
using Pondhawk.Api.Endpoints;
using Pondhawk.Api.Filters;
using Pondhawk.Mediator;

public sealed class OrderModule(IMediator mediator) : IEndpointModule
{
    public string BasePath => "/orders";

    public void Configure(RouteGroupBuilder group) =>
        group.AddEndpointFilter<ResponseEndpointFilter>();   // Response<T> -> Ok/JSON or ProblemDetails

    public void AddRoutes(IEndpointRouteBuilder app) =>
        // handler returns Response<Order>; the filter renders it
        app.MapGet("/{id:int}", (int id) => mediator.SendAsync(new GetOrder(id)));
}
```

Wire it up in `Program.cs`:

```csharp
using Pondhawk.Api;
using Pondhawk.Api.Endpoints;
using Pondhawk.Api.Identity;
using Pondhawk.Api.Json;
using Pondhawk.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPondhawkApi();                                    // IHttpContextAccessor + scoped IRequestContext
builder.Services.AddPondhawkJson();                                  // compact resolver + Pascal naming
builder.Services.AddGatewayTokenAuthentication(config["GatewaySigningKey"]!); // HS256 X-Gateway-Identity-Token
builder.Services.AddMediator(typeof(OrderModule).Assembly);          // Pondhawk.Core mediator
builder.Services.AddEndpointModules(typeof(OrderModule).Assembly);   // discover IEndpointModules

var app = builder.Build();

app.UseDiagnosticsMonitor();      // register EARLY: whole-pipeline begin/end + elapsed
app.UseAuthentication();
app.UseDiagnosticsEnrichment();   // after auth: populate IRequestContext (correlation/caller/token)
app.UseRequestLogging();          // deep: full request dump when diagnostics is debug-enabled

app.MapEndpointModules("/api");   // maps each module's group under /api
app.Run();
```

Gateway authentication is inbound-only: one scheme (`IdentityConstants.Scheme`) with two handlers — token mode (signed HS256 JWT) or header mode (`AddGatewayHeaderAuthentication()`, unsigned JSON claim set). See the [package README](src/Pondhawk.Api/README.md) for the full concern list.

---

## Dependency Graph

```
Pondhawk.Core              (foundation — mediator, configuration, pipeline, utilities, exceptions)
Pondhawk.Logging           (standalone — logging API + ILoggerSource)
Pondhawk.Logging.Watch     (→ Pondhawk.Logging — Watch sink + switching + switch-aware source)

Pondhawk.Rules             (standalone)
    ^
    |
Pondhawk.Rules.EFCore ──> Pondhawk.Rules

Pondhawk.Hosting           (standalone)

Pondhawk.Api ──> Pondhawk.Core, Pondhawk.Logging, Pondhawk.Rules
    (ASP.NET web kit — endpoint modules, Response<T> -> ProblemDetails, gateway identity, diagnostics)
```

## Building & Testing

The repository uses [Cake Frosting](https://cakebuild.net/) for build orchestration.

```bash
# Build all projects
dotnet build pondhawk-tools.slnx

# Run all tests
dotnet test pondhawk-tools.slnx

# Cake targets: Clean, Restore, Build, Test, Pack, Push
dotnet run --project build/Build.csproj -- --target=Test
dotnet run --project build/Build.csproj -- --target=Pack --package-version=1.0.0
```

## License

MIT &copy; [Pond Hawk Technologies Inc.](https://github.com/pondhawk)
