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
  <a href="https://www.nuget.org/packages/Pondhawk.Hosting"><img src="https://img.shields.io/nuget/v/Pondhawk.Hosting?label=Hosting" alt="Pondhawk.Hosting" /></a>
  <a href="https://www.nuget.org/packages/Pondhawk.Api"><img src="https://img.shields.io/nuget/v/Pondhawk.Api?label=Api" alt="Pondhawk.Api" /></a>
</p>

---

## Overview

Pondhawk Tools is a collection of class libraries built by [Pond Hawk Technologies](https://github.com/pondhawk). Each package is independently versioned and can be adopted on its own.

| Package | Description |
|---------|-------------|
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

An ASP.NET Core web kit built on Pondhawk.Core, Pondhawk.Logging, and the external Pondhawk.Rules NuGet package. Endpoints are grouped into `IEndpointModule`s that dispatch to the mediator; handlers return a `Response<T>` envelope and stay transport-agnostic, while `ResponseEndpointFilter` renders it to `Ok`/JSON on success or a ProblemDetails (`application/problem+json`) on failure — with the `ErrorKind` mapped to the right HTTP status.

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

Pondhawk.Hosting           (standalone)

Pondhawk.Api ──> Pondhawk.Core, Pondhawk.Logging (+ external Pondhawk.Rules NuGet package)
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
