<p align="center">
  <img src="pht-small-logo.png" alt="Pondhawk Tools" width="120" />
</p>

<h1 align="center">Pondhawk Tools</h1>

<p align="center">
  A modular .NET toolkit for rule evaluation, resource querying, structured logging, and service lifecycle management.
</p>

<p align="center">
  <a href="https://github.com/pondhawk/tools/actions/workflows/build.yml"><img src="https://github.com/pondhawk/tools/actions/workflows/build.yml/badge.svg" alt="Build" /></a>
  <img src="https://img.shields.io/badge/.NET-10.0-512bd4" alt=".NET 10" />
  <img src="https://img.shields.io/badge/license-MIT-blue" alt="MIT License" />
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/Pondhawk.Core"><img src="https://img.shields.io/nuget/v/Pondhawk.Core?label=Core" alt="Pondhawk.Core" /></a>
  <a href="https://www.nuget.org/packages/Pondhawk.Watch"><img src="https://img.shields.io/nuget/v/Pondhawk.Watch?label=Watch" alt="Pondhawk.Watch" /></a>
  <a href="https://www.nuget.org/packages/Pondhawk.Rules"><img src="https://img.shields.io/nuget/v/Pondhawk.Rules?label=Rules" alt="Pondhawk.Rules" /></a>
  <a href="https://www.nuget.org/packages/Pondhawk.Rules.EFCore"><img src="https://img.shields.io/nuget/v/Pondhawk.Rules.EFCore?label=Rules.EFCore" alt="Pondhawk.Rules.EFCore" /></a>
  <a href="https://www.nuget.org/packages/Pondhawk.Rql"><img src="https://img.shields.io/nuget/v/Pondhawk.Rql?label=Rql" alt="Pondhawk.Rql" /></a>
  <a href="https://www.nuget.org/packages/Pondhawk.Hosting"><img src="https://img.shields.io/nuget/v/Pondhawk.Hosting?label=Hosting" alt="Pondhawk.Hosting" /></a>
</p>

---

## Overview

Pondhawk Tools is a collection of class libraries built by [Pond Hawk Technologies](https://github.com/pondhawk). Each package is independently versioned and can be adopted on its own.

| Package | Description |
|---------|-------------|
| [**Pondhawk.Rules**](src/Pondhawk.Rules/README.md) | Forward-chaining rule engine with type-based fact matching, scoring, and validation |
| [**Pondhawk.Rules.EFCore**](src/Pondhawk.Rules.EFCore/README.md) | EF Core `SaveChangesInterceptor` that validates entities through Rules before save |
| [**Pondhawk.Rql**](src/Pondhawk.Rql/README.md) | Resource Query Language — filtering DSL with fluent builder, parser, and SQL/LINQ serialization |
| [**Pondhawk.Core**](src/Pondhawk.Core/README.md) | Shared foundation — mediator, configuration modules, pipeline infrastructure, utilities, exceptions |
| [**Pondhawk.Watch**](src/Pondhawk.Watch/README.md) | Serilog logging API + sink with Channel-based batching and circuit-breaker resilience |
| [**Pondhawk.Hosting**](src/Pondhawk.Hosting/README.md) | `AddSingletonWithStart<T>()` pattern for co-locating service registration with startup logic |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.103 or later)

### Installation

Stable releases are published to [nuget.org](https://www.nuget.org/profiles/pondhawk). Install the packages you need:

```bash
dotnet add package Pondhawk.Rules
dotnet add package Pondhawk.Rql
dotnet add package Pondhawk.Watch
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

## Pondhawk.Rql

A Resource Query Language with a fluent builder, text parser, and multiple serialization targets (RQL text, LINQ expressions, parameterized SQL).

### Fluent Builder

Build filters with strongly-typed lambda expressions:

```csharp
var filter = RqlFilterBuilder<Product>
    .Where(p => p.Category).Equals("Electronics")
    .And(p => p.Price).GreaterThan(99)
    .And(p => p.InStock).Equals(true);
```

### Supported Operators

| Builder Method | RQL Syntax | SQL Output |
|----------------|-----------|------------|
| `.Equals(v)` | `eq(Field,v)` | `Field = @p` |
| `.NotEquals(v)` | `ne(Field,v)` | `Field <> @p` |
| `.LesserThan(v)` | `lt(Field,v)` | `Field < @p` |
| `.GreaterThan(v)` | `gt(Field,v)` | `Field > @p` |
| `.LesserThanOrEqual(v)` | `le(Field,v)` | `Field <= @p` |
| `.GreaterThanOrEqual(v)` | `ge(Field,v)` | `Field >= @p` |
| `.Between(a, b)` | `bt(Field,a,b)` | `Field between @a and @b` |
| `.In(a, b, c)` | `in(Field,a,b,c)` | `Field in (@a,@b,@c)` |
| `.NotIn(a, b, c)` | `ni(Field,a,b,c)` | `Field not in (@a,@b,@c)` |
| `.StartsWith(v)` | `sw(Field,v)` | `Field like 'v%'` |
| `.Contains(v)` | `cn(Field,v)` | `Field like '%v%'` |
| `.EndsWith(v)` | `ew(Field,v)` | `Field like '%v'` |
| `.IsNull()` | `nu(Field)` | `Field is null` |
| `.IsNotNull()` | `nn(Field)` | `Field is not null` |

Values support `string`, `int`, `long`, `decimal`, `DateTime`, and `bool` types.

### Parsing

Parse RQL text back into a filter AST:

```csharp
var tree = RqlLanguageParser.ToCriteria("(eq(Name,'John'),gt(Age,30))");
```

Value prefixes in RQL text:
- Strings: `'value'` (escape single quotes as `''`)
- DateTime: `@2025-01-15T00:00:00Z`
- Decimal: `#99.95`
- Integers and booleans: bare values (`30`, `true`)

### Serialization

Serialize filters to three different output formats:

```csharp
var filter = RqlFilterBuilder<Product>
    .Where(p => p.Category).Equals("Electronics")
    .And(p => p.Price).GreaterThan(99);

// RQL text
string rql = filter.ToRql();
// "(eq(Category,'Electronics'),gt(Price,99))"

// Compiled LINQ predicate
Func<Product, bool> predicate = filter.ToLambda<Product>();
var matches = products.Where(predicate);

// Expression tree (for EF Core / IQueryable)
Expression<Func<Product, bool>> expr = filter.ToExpression<Product>();
var results = dbContext.Products.Where(expr);

// Parameterized SQL
var (sql, parameters) = filter.ToSqlQuery<Product>();
// sql:        "select * from Product where Category = {0} and Price > {1}"
// parameters: ["Electronics", 99]

// WHERE clause only
var (where, args) = filter.ToSqlWhere();
// where: "Category = {0} and Price > {1}"

// Human-readable English description
string description = filter.ToDescription();
// "Category equals 'Electronics' and Price is greater than 99"
```

Case-insensitive string matching is supported via the `insensitive` parameter:

```csharp
var predicate = filter.ToLambda<Product>(insensitive: true);
```

### Introspection

Build filters automatically from criteria objects decorated with `[Criterion]`:

```csharp
public class ProductSearch : BaseCriteria
{
    [Criterion(Operation = RqlOperator.Contains)]
    public string? Name { get; set; }

    [Criterion(Operation = RqlOperator.Equals)]
    public string? Category { get; set; }

    [Criterion(Name = "Price", Operand = OperandKind.From)]
    public decimal? MinPrice { get; set; }

    [Criterion(Name = "Price", Operand = OperandKind.To)]
    public decimal? MaxPrice { get; set; }
}

// Build filter from populated criteria
var search = new ProductSearch { Category = "Electronics", MinPrice = 50, MaxPrice = 200 };
var filter = RqlFilterBuilder<Product>.Create().Introspect(search);
// Produces: eq(Category,'Electronics'), bt(Price,#50,#200)
```

### Untyped Builder

For dynamic scenarios where the target type isn't known at compile time:

```csharp
var filter = RqlFilterBuilder
    .Where("Status").Equals("Active")
    .And("CreatedDate").GreaterThan(DateTime.UtcNow.AddDays(-30));

var (sql, args) = filter.ToSqlWhere();
```

---

## Other Packages

### Pondhawk.Core

Shared foundation with mediator, configuration-driven DI modules, pipeline infrastructure, type utilities, and common exception types.

```csharp
// Mediator — CQRS-style request dispatch with pipeline behaviors
services.AddMediator(typeof(MyHandler).Assembly);
services.AddPipelineBehavior(typeof(LoggingBehavior<,>));

await mediator.SendAsync(new CreateOrderCommand { ... });

// Configuration — bind modules from IConfiguration and register services
services.AddServiceModule<DatabaseModule>(configuration);

// Pipeline — composable step-based execution
services.AddPipelineFactory();
services.AddPipeline<OrderContext>(steps => steps
    .Add<ValidateStep>()
    .Add<CalculateTaxStep>()
    .Add<SaveStep>());
```

### Pondhawk.Watch

Serilog logging API + `ILogEventSink` with unbounded `Channel<T>` batching and circuit-breaker resilience. Provides `GetLogger()` on any object, disposable method tracing, typed payload logging, and the Watch sink.

```csharp
using Pondhawk.Watch;
using Serilog;

// Configure Serilog — Watch Server controls log levels via switches
Log.Logger = new LoggerConfiguration()
    .UseWatch("http://localhost:11000", "MyApp")
    .CreateLogger();

// Logging API
var log = this.GetLogger();
using (log.EnterMethod())
{
    log.Inspect("orderId", orderId);        // logs "orderId = 123" at Debug
    log.LogObject("payload", order);        // serializes object to JSON
    log.LogSql("query", sqlText);           // typed payload with syntax hint
}
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

---

## Dependency Graph

```
Pondhawk.Core              (foundation — mediator, configuration, pipeline, utilities, exceptions)
Pondhawk.Watch             (standalone — logging API, Serilog sink)

Pondhawk.Rules             (standalone)
    ^
    |
Pondhawk.Rules.EFCore ──> Pondhawk.Rules

Pondhawk.Rql               (standalone)
Pondhawk.Hosting           (standalone)
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
