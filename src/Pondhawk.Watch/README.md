# Pondhawk.Watch

A Serilog `ILogEventSink` with Channel-based batching for the Watch structured logging pipeline. Provides rich structured logging with method tracing, object serialization, and multiple sink targets.

## Quick Start

### Configure Serilog for Watch

```csharp
using Pondhawk.Watch;
using Serilog;

// Recommended — Watch Server controls log levels via switches
Log.Logger = new LoggerConfiguration()
    .UseWatch("http://localhost:11000", "MyApp")
    .CreateLogger();

// Advanced — manual MinimumLevel and sink configuration
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Watch("http://localhost:11000", "MyApp")
    .CreateLogger();
```

### Running alongside other sinks (e.g. Grafana Loki)

The Watch sink is a standard Serilog `ILogEventSink`, so it composes with any off-the-shelf
sink — every event fans out to all configured sinks. A common production setup pairs Watch
(rich, live developer view) with [Grafana Loki](https://grafana.com/oss/loki/) (durable,
queryable store) via [`Serilog.Sinks.Grafana.Loki`](https://www.nuget.org/packages/Serilog.Sinks.Grafana.Loki):

```csharp
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;

Log.Logger = new LoggerConfiguration()
    .UseWatch("http://watch:11000", "MyApp")           // Verbose; Watch Server drives filtering via switches
    .WriteTo.GrafanaLoki(
        "http://loki:3100",
        labels: new[] { new LokiLabel { Key = "app", Value = "myapp" } },
        restrictedToMinimumLevel: LogEventLevel.Information)   // keep Verbose out of Loki storage
    .CreateLogger();
```

Two things to keep in mind when adding a second sink next to Watch:

- **`UseWatch` sets `MinimumLevel.Verbose()`** so the Watch Server can control filtering through
  switches. That global minimum applies to *every* sink, so a storage sink like Loki will receive
  Verbose too unless you constrain it per-sink with `restrictedToMinimumLevel:` (as above).
- **Label cardinality.** The logging API attaches a `Watch.PayloadContent` property that can hold a
  large JSON payload (from `LogObject`/`LogJson`). Keep it — and any other high-cardinality property —
  in the log line body, never promoted to a Loki label. Reserve labels for low-cardinality dimensions
  (`app`, `env`, `service`); indexing a large or unique value as a label will blow up Loki's index.

### Use the Logging API

The logging API is a set of extensions on Serilog's `ILogger`. Obtain a logger the standard
Serilog way — `Log.ForContext<T>()` sets the `SourceContext` to the type name — then call the
Watch extensions on it.

```csharp
using Pondhawk.Watch;
using Serilog;

public class OrderService
{
    private readonly ILogger _logger = Log.ForContext<OrderService>();

    public void ProcessOrder(int orderId)
    {
        using var _ = _logger.EnterMethod();

        _logger.Debug("Loading order {OrderId}", orderId);
        var order = LoadOrder(orderId);
        _logger.LogObject(order);
    }
}
```

### Method Tracing

```csharp
public async Task ProcessAsync(int orderId)
{
    using var _ = _logger.EnterMethod();
    // Logs "Entering ProcessAsync" at Verbose level (the class comes from SourceContext)
    // On dispose: "Exiting ProcessAsync (elapsed ms)"
}
```

### Object Serialization

```csharp
logger.LogObject(order);                       // Serialize to JSON payload
logger.LogObject("Fetched Order", order);      // With custom title

// Typed payloads with syntax highlighting hints
logger.LogJson("API Response", jsonString);
logger.LogSql("Query", sqlString);
logger.LogXml("Configuration", xmlString);
logger.LogYaml("Settings", yamlString);
logger.LogText("Output", textString);
```

### Sensitive Data Masking

```csharp
public class Credentials
{
    public string Username { get; set; }

    [Sensitive]
    public string Password { get; set; }  // Logged as "Sensitive - HasValue: true"
}
```

## Key Components

- **WatchSink** -- `ILogEventSink` with unbounded `Channel` batching. Converts Serilog events to Watch `LogEvent` instances.
- **Switching** -- Dynamic log level control via `ISwitch`/`ISwitchSource` with pattern matching (longest prefix wins).
- **Console Sink** -- Colored console output.
- **Monitor Sink** -- Accumulates events for testing.
- **HTTP Sink** -- Posts event batches to Watch Server with circuit breaker and critical event buffering.
- **LogEvent / LogEventBatch** -- MemoryPack-serializable event model with Brotli compression.

## Architecture

Events flow: Serilog `ILogger` -> WatchSink (Channel queue) -> Background batch task -> Event sink (Console / HTTP / Monitor).

Switch-based filtering checks the source context pattern against configured switches. Longest prefix match wins. Version-based invalidation ensures cached loggers see switch updates without recreation.

Fully standalone -- no dependency on Pondhawk.Core. Logging API types (`SerilogExtensions`, `MethodLogger`, `PayloadType`, `SensitiveAttribute`) are included directly.

## Documentation

See [CLAUDE.md](CLAUDE.md) for detailed AI development guidance and logging conventions.
