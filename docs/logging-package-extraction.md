# Build Brief: Extract `Pondhawk.Logging`, rename Watch to `Pondhawk.Logging.Watch`

Status: **in progress** — Phase 1 **done** (Pondhawk.Logging builds 0/0). Branch: `refactor/extract-pondhawk-logging`.

> Note: `ILoggerSource`'s method is named **`CreateLogger<T>()` / `CreateLogger(Type)` / `CreateLogger(string)`**, not `For<T>()` — `For` trips analyzer rule CA1716 (collides with the VB `For` keyword). Usage: `loggers.CreateLogger<GetOrderHandler>()`.

## Goal / outcome

- **`Pondhawk.Logging`** — Serilog-only package: the ergonomic API (`EnterMethod`/`Inspect`/`LogObject`/`Log*`), `ILoggerSource` + a plain `SerilogLoggerSource`, and the neutralized property contract. No sink, no switching.
- **`Pondhawk.Logging.Watch`** — the Watch provider: sink, switching, `WatchLogger` (switch-aware `ILogger`), `WatchLoggerSource`. References `Pondhawk.Logging`.
- A consuming app depends on `Pondhawk.Logging` always; on `.Watch` only while using Watch — and can supply its own `ILoggerSource` with handlers unchanged.

### Why
The logging API has zero coupling to the sink/switching (verified: `SerilogExtensions`/`MethodLogger` reference nothing sink-related). Splitting it out lets an app drop the Watch package entirely and provide its own `ILoggerSource` while keeping the same `EnterMethod`/`Inspect`/`LogObject` calls in handlers. Watch becomes a swappable provider, not a per-handler dependency.

### Switch-aware guard (Option B, folded in)
`LogObject`/`LogPayload`/`EnterMethod` guard on `logger.IsEnabled(Verbose)`, which is always true under `UseWatch` (it sets `MinimumLevel.Verbose()`), so payloads serialize even for switch-dropped categories — wasted hot-path work. Fix: `WatchLogger : ILogger` overrides the real interface member `IsEnabled(level)` to consult the live switch (`switches.Lookup(category).Level`). Because the extensions already gate on `IsEnabled` (which dispatches virtually, unlike the extension methods themselves), the entire existing API becomes switch-aware with no changes to the extensions, and callers hold only `Serilog.ILogger`. The dead `WatchSwitchConfig` is removed. `IWatchLoggerFactory`/`WatchLoggerSource` shares one `SwitchSource` with the sink (no ambient state).

## Neutralized property contract (decided)

`WatchPropertyNames` → **`LogPropertyNames`**, made **public**, in `Pondhawk.Logging`:

| old | new |
|---|---|
| `Watch.Nesting` | `Pondhawk.Nesting` |
| `Watch.PayloadType` | `Pondhawk.PayloadType` |
| `Watch.PayloadContent` | `Pondhawk.PayloadContent` |
| `Watch.CorrelationId` | `Pondhawk.CorrelationId` |
| baggage `watch.correlation` | `pondhawk.correlation` |

Plus `LogPropertyNames.Prefix = "Pondhawk."`. Safe for `watch-server`: these are the internal extensions↔sink handshake, mapped into the `LogEvent` model by `ConvertEvent` — not the wire format. **`WatchSink.BuildStructuredPayload`'s `StartsWith("Watch.")` filter must switch to `LogPropertyNames.Prefix`.**

## Phases

### Phase 1 — Create `Pondhawk.Logging` (pure Serilog API)
- New `src/Pondhawk.Logging/Pondhawk.Logging.csproj` — `net10.0`, root namespace `Pondhawk.Logging`, deps `Serilog` + `System.Text.Json`, own `version.json`, `InternalsVisibleTo("Pondhawk.Logging.Tests")`.
- Move Watch → Logging (namespaces `Pondhawk.Watch*` → `Pondhawk.Logging*`): `SerilogExtensions`, `MethodLogger`, `Utilities/TypeExtensions`, `Serializers/*` (5), `PayloadType`, `SensitiveAttribute`, `CorrelationManager`, `WatchPropertyNames`→`LogPropertyNames` (public, neutralized). Split `GlobalUsings`.
- New: `ILoggerSource`, `SerilogLoggerSource`.
- Verify: `dotnet build src/Pondhawk.Logging`. (Watch is intentionally broken until Phase 2.)

### Phase 2 — Rename `Pondhawk.Watch` → `Pondhawk.Logging.Watch`
- Rename dir + csproj (assembly/package id/root namespace); add `ProjectReference` → `Pondhawk.Logging`.
- Update remaining namespaces + `using Pondhawk.Logging;`; `WatchSink` uses public `LogPropertyNames`/`PayloadType`; update the `StartsWith("Watch.")` filter to `LogPropertyNames.Prefix`.
- `InternalsVisibleTo("Pondhawk.Logging.Watch.Tests")`.
- Verify: build both.

### Phase 3 — Switch-aware logger + source in `.Watch`
- `internal sealed class WatchLogger(ILogger inner, string category, SwitchSource switches) : ILogger` — `IsEnabled` = switch-aware; guard `Write` overloads; `ForContext` returns `WatchLogger`.
- `public sealed class WatchLoggerSource(ILogger root, SwitchSource switches) : ILoggerSource`.
- `UseWatch` overload exposing the `SwitchSource` so the root wires sink + source to one instance.
- Verify: build + focused switch-aware test.

### Phase 4 — Split & extend tests
- `test/Pondhawk.Logging.Tests` ← `SerilogExtensionsTests`, `MethodLoggerTests`, `TypeExtensionsTests`, `JsonObjectSerializerTests`, `JsonConvertersTests`, `CorrelationManagerTests`, `Support/CollectingSink`; add `SerilogLoggerSourceTests`.
- `test/Pondhawk.Watch.Tests` → `test/Pondhawk.Logging.Watch.Tests`; keep sink/switch/event tests; add `WatchLoggerTests` + `WatchLoggerSourceTests`.
- Verify: `dotnet test` whole solution.

### Phase 5 — Solution, packaging, docs
- `pondhawk-tools.slnx` add both new projects; `Directory.Packages.props` (no new versions); update `CLAUDE.md`, Watch `README`/`CLAUDE.md`, add `Pondhawk.Logging` README.
- Verify: full build + test.

### Phase 6 — Cross-repo `watch-server` (separate, coordinate)
- `PackageReference` `Pondhawk.Watch` → `Pondhawk.Logging.Watch`; `using` updates. Done in lockstep. Not touched without explicit authorization.

## Risks
- Breaking rename → contained to a branch; `watch-server` updated in lockstep (Phase 6).
- Property neutralization must be atomic across writer (extensions) + reader (`WatchSink` filter).
- No behavior change intended — the moved 157 tests are the safety net.

## Verification gate
`dotnet build pondhawk-tools.slnx` + `dotnet test pondhawk-tools.slnx` green after Phases 1(Logging only), 2, 4, 5.
