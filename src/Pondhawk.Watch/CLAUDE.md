# Pondhawk.Watch - AI Development Guide

## Overview

Pondhawk.Watch is a Serilog `ILogEventSink` with Channel-based batching, plus the full Watch logging API (`SerilogExtensions`). It provides structured logging with method tracing, object serialization, typed payloads, and HTTP sink delivery with circuit-breaker resilience.

Targets `net10.0` (single target — no conditional compilation). Fully standalone — no dependency on Pondhawk.Core.

---

## Logging Guidelines

**Logging is the primary debugging tool.** You cannot attach a debugger in production, but you can always read logs. Well-structured logging tells you exactly what happened and why.

### 1. Start Methods with EnterMethod

Most methods should begin with `EnterMethod()`. Only the simplest methods (one-liners, trivial getters) skip this.

```csharp
using Pondhawk.Watch;
using Serilog;

public class OrderService
{
    private readonly ILogger _logger = Log.ForContext<OrderService>();

    public async Task<Order> ProcessOrderAsync(int orderId)
    {
        using var _ = _logger.EnterMethod();

        _logger.Debug("Loading order from database");
        var order = await _repository.GetOrderAsync(orderId);
        _logger.LogObject(order);

        return order;
    }
}
```

- Obtain a logger the standard Serilog way — `Log.ForContext<T>()` sets `SourceContext` to the type name
- Use discard `_` for `EnterMethod()` return value
- Creates collapsible hierarchy in log viewers with automatic timing

### 2. Logging IS Comments, Comments ARE Logging

**Do not write comments. Write log statements instead.**

The log serves as both runtime documentation AND debugging information. Comments are invisible in production; logs are not.

```csharp
// BAD - Comment invisible in production
// Validate the order before processing
if (!order.IsValid)
    return null;

// GOOD - Log visible in production, serves as documentation
logger.Debug("Validating order before processing");
if (!order.IsValid)
{
    logger.Debug("Order validation failed");
    return null;
}
```

### 3. Log Calculated and Fetched Values

When you calculate a value or fetch it from somewhere (database, API, config), log it.

```csharp
var discount = CalculateDiscount(customer);
logger.Inspect("discount", discount);

var user = await _repository.GetUserAsync(userId);
logger.Inspect("user.Email", user?.Email ?? "not found");
```

### 4. Use LogObject for Complex Types

When fetching objects from a database or receiving complex DTOs, use `LogObject` to capture the full state.

```csharp
var order = await _db.Orders.FindAsync(orderId);
logger.LogObject(order);

var response = await _client.GetAsync<ApiResponse>(url);
logger.LogObject(response);
```

`LogObject` uses `JsonObjectSerializer` which:
- **Catches exceptions from property getters** — Some objects (e.g., MemoryStream) have properties that throw when accessed. The serializer catches these and returns defaults.
- **Respects [Sensitive] attribute** — Properties marked with `[Sensitive]` are masked.

### 5. Mark Sensitive Data with [Sensitive]

Never log passwords, API keys, tokens, or PII. Mark sensitive properties with the `[Sensitive]` attribute:

```csharp
public class UserCredentials
{
    public string Username { get; set; }

    [Sensitive]
    public string Password { get; set; }

    [Sensitive]
    public string ApiKey { get; set; }
}

// Logs: { "Username": "jsmith", "Password": "Sensitive - HasValue: true", "ApiKey": "Sensitive - HasValue: true" }
logger.LogObject(credentials);
```

### 6. Provide Context for Problem-Solving

Include relevant IDs, states, and values.

```csharp
logger.Debug("Processing payment for Order {OrderId}, Amount {Amount}, Customer {CustomerId}",
    order.Id, order.Total, order.CustomerId);
```

### 7. Exception Context is Critical

```csharp
catch (Exception ex)
{
    logger.Error(ex, "Failed to process order {OrderId} for customer {CustomerId} with amount {Amount}",
        orderId, customerId, amount);
    throw;
}
```

### Summary

| Principle | Practice |
|-----------|----------|
| Start methods | `using var _ = _logger.EnterMethod();` |
| Get a logger | `private readonly ILogger _logger = Log.ForContext<MyType>();` |
| Replace comments | `logger.Debug("Explanation of what's happening");` |
| Log values | `logger.Inspect("x", x);` |
| Log complex objects | `logger.LogObject(dto);` |
| Mark sensitive data | `[Sensitive]` attribute on properties |
| Provide context | Include IDs, states, relevant values |
| Exception handling | Include context: IDs, values, state at time of failure |

---

## Key Concepts

### Switches (Dynamic Log Levels)

- Switches control logging level and color per category pattern
- Fetched from Watch Server via HTTP (`WatchSwitchSource`), cached with version-based invalidation
- Pattern matching: longest prefix wins ("MyApp.Data" beats "MyApp")

### Color (UI Visualization)

- Color comes from Switch configuration, NOT from application code
- Applied automatically to every LogEvent
- Used in Watch Server UI for visual category grouping

### Nesting (Method Tracing)

- `EnterMethod()` sets Nesting = 1, dispose sets Nesting = -1
- Watch viewers render as collapsible method hierarchy
- Includes elapsed time measurement

### PayloadType (Syntax Highlighting)

- Json, Sql, Xml, Yaml, Text for UI syntax highlighting
- Use `LogJson()`, `LogSql()`, `LogXml()` etc. for explicit types
- `LogObject()` automatically uses Json type

## Extension Method Reference

### Method Tracing

```csharp
using var scope = logger.EnterMethod();   // extension on Serilog ILogger
// Logs entry with Nesting=1, exit with Nesting=-1 and timing
```

### Logger Creation

```csharp
// Standard Serilog acquisition; sets SourceContext to the type name.
private readonly ILogger _logger = Log.ForContext<MyType>();
```

### Typed Payloads (all targets)

```csharp
logger.LogObject(dto);              // Serializes to JSON
logger.LogJson("Title", jsonStr);   // Raw JSON with highlighting
logger.LogSql("Query", sqlStr);     // SQL syntax highlighting
logger.LogXml("Config", xmlStr);    // XML syntax highlighting
logger.LogYaml("Data", yamlStr);    // YAML syntax highlighting
logger.LogText("Output", textStr);  // Plain text
logger.Inspect("name", value);      // Logs "name = value" at Debug
```

## Sink Configuration

```csharp
using Pondhawk.Watch;
using Serilog;

// Recommended — Watch Server controls log levels via switches
Log.Logger = new LoggerConfiguration()
    .UseWatch("http://localhost:11000", "MyApp")
    .CreateLogger();

// With options
Log.Logger = new LoggerConfiguration()
    .UseWatch("http://localhost:11000", "MyApp", opts =>
    {
        opts.BatchSize = 50;
        opts.PollInterval = TimeSpan.FromSeconds(15);
    })
    .CreateLogger();

// Advanced — manual control over MinimumLevel
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Watch("http://localhost:11000", "MyApp")
    .CreateLogger();
```

## Architecture Notes

### Channel-Based Batching

- `WatchSink.Emit()` writes to an unbounded channel (non-blocking)
- Background task drains channel by batch size or flush interval
- Converts Serilog `LogEvent` → Watch `LogEvent` with switch-based filtering
- Flushes remaining events on dispose

### Circuit Breaker (HTTP Sink)

- Opens after N consecutive failures (`FailureThreshold`, default: 3)
- Critical events (Warning/Error) buffered during outage (`MaxCriticalBufferSize`)
- Non-critical events dropped
- Exponential backoff with max delay

### Logging API ↔ Sink Communication

The logging API (`SerilogExtensions`) writes well-known Serilog property names:
- `Watch.Nesting` — method tracing depth (+1 enter, -1 exit)
- `Watch.PayloadType` — int value of `PayloadType` enum
- `Watch.PayloadContent` — serialized payload string

`WatchSink.ConvertEvent()` reads these properties from the Serilog `LogEvent` and maps them to the Watch `LogEvent` model.

## Performance Guidelines

1. **Disabled Levels**: `WatchSwitchConfig.IsEnabled()` check is near-zero cost
2. **Enabled Levels**: LogEvent allocation, JSON serialization for payloads
3. **Batching**: Events queued to channel, batched for HTTP delivery
4. **Hot Path**: Avoid string interpolation before level check

```csharp
// Good - no allocation if disabled
logger.Debug("User {UserId} logged in", userId);

// Bad - string allocated even if disabled
logger.Debug($"User {userId} logged in");
```

## Project Structure

```
src/Pondhawk.Watch/
  # Event model + serialization
  PayloadType.cs                        # None, Json, Sql, Xml, Text, Yaml
  WatchPropertyNames.cs                 # Serilog property name constants
  LogEvent.cs                           # Core event model (MemoryPackable)
  LogEventBatch.cs                      # Batch container
  LogEventBatchSerializer.cs            # MemoryPack+Brotli wire; JSON for debug/testing
  LogEventBatchContext.cs               # STJ source-gen context (JSON debug/testing)

  # Sink + configuration
  WatchSink.cs                          # ILogEventSink with channel batching + circuit breaker
  WatchSinkExtensions.cs                # Serilog LoggerConfiguration extensions (UseWatch / Watch)
  WatchSinkOptions.cs                   # Options for the UseWatch / Watch convenience methods

  # Switching
  Switch.cs                             # Switch model (Pattern, Tag, Level, Color)
  SwitchDef.cs                          # Switch definition DTO
  SwitchDto.cs                          # Wire format for HTTP switch updates
  SwitchesResponse.cs                   # HTTP response model
  SwitchSource.cs                       # Local switch source with pattern matching
  WatchSwitchSource.cs                  # Polls Watch Server for switch configuration

  # Logging API
  SerilogExtensions.cs                  # EnterMethod, Inspect, LogObject, LogJson, etc.
  MethodLogger.cs                       # ILogger wrapper with method tracing
  CorrelationManager.cs                 # Activity-based correlation ID management
  SensitiveAttribute.cs                 # [Sensitive] for masking properties

  GlobalUsings.cs                       # Shared usings for the project

  Serializers/
    IObjectSerializer.cs                # Object to payload abstraction
    JsonObjectSerializer.cs             # System.Text.Json with safe property access
    LoggingJsonTypeInfoResolver.cs      # Safe getter wrapping + [Sensitive] handling
    AttributeJsonConverter.cs           # Attribute → { "Name": "..." }
    TypeJsonConverter.cs                # Type → { "Name": "..." }

  Utilities/
    TypeExtensions.cs                   # GetConciseName/GetConciseFullName with caching
```

## Common Mistakes

- Don't use string interpolation: `$"User {user}"` allocates even when disabled
- Do use structured logging: `"User {UserId}", userId`
- Do use `EnterMethod()` for method-level tracing
- Do use appropriate PayloadType for syntax highlighting
- Don't set color in application code — it comes from Switch configuration
