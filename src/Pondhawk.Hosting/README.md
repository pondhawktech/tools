# Pondhawk.Hosting

Lightweight service lifecycle management for `Microsoft.Extensions.Hosting`. Co-locate service start/stop logic with DI registration. Standalone -- no dependency on other Pondhawk packages.

## Quick Start

### Register Services with Start Logic

```csharp
using Pondhawk.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Sync start
builder.Services.AddSingletonWithStart<MyService>(s => s.Initialize());

// Sync start + stop
builder.Services.AddSingletonWithStart<CacheService>(
    s => s.WarmUp(),
    s => s.Flush());

// Async start + stop with cancellation
builder.Services.AddSingletonWithStart<BackgroundProcessor>(
    (s, ct) => s.StartAsync(ct),
    (s, ct) => s.StopAsync(ct));

var app = builder.Build();
await app.RunAsync();
// Start actions fire on host startup
// Stop actions fire in reverse order on shutdown
```

### With the Rules Engine

```csharp
builder.Services.AddSingletonWithStart<RuleSetFactory>(f => f.Start());
```

### File-Based Lifecycle Signaling

`AppLifecycleService` is an `IHostedService` that signals lifecycle state through flag
files and watches for an external shutdown request — handy when an orchestrator or script
needs to observe or stop the app out of band.

```csharp
// Defaults the flag directory to AppContext.BaseDirectory
builder.Services.AddHostedService(sp => new AppLifecycleService(
    sp.GetRequiredService<IHostApplicationLifetime>(),
    sp.GetRequiredService<ILoggerFactory>(),
    flagDirectory: "/var/run/myapp"));
```

- On start it clears stale flags, then writes `started.flag` when the host has started and
  `stopped.flag` when it has stopped.
- A `FileSystemWatcher` watches the directory for an externally-created `muststop.flag`;
  when one appears it requests a graceful shutdown via `IHostApplicationLifetime.StopApplication()`.

## How It Works

- `AddSingletonWithStart<T>` registers your service as a singleton and stores the start/stop lambdas.
- `ServiceStarterHostedService` (auto-registered) implements `IHostedService`.
- On `StartAsync`, it resolves each registered service and calls its start lambda.
- On `StopAsync`, it calls stop lambdas in reverse registration order.
- Falls back to `NullLoggerFactory` when `AddLogging()` hasn't been called (e.g. in tests).
