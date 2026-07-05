# Build Brief: Extract `Pondhawk.Logging`, rename Watch to `Pondhawk.Logging.Watch`

Status: **Phases 1–5 DONE** (in-repo work complete). Full solution builds + **1044 tests pass, 0 failures**. Only Phase 6 (cross-repo `watch-server`) remains, and is out-of-repo / explicitly-authorized. Branch: `refactor/extract-pondhawk-logging`.

Phase 5 notes: `pondhawk-tools.slnx` updated (added both new src + test projects, dropped old Watch). Root `CLAUDE.md` (build commands, architecture split, dependency graph, watch-server note) updated. Package docs rewritten: `Pondhawk.Logging.Watch` README/CLAUDE.md reframed as the provider; new `Pondhawk.Logging` README + CLAUDE.md own the API + `ILoggerSource` guide; `Pondhawk.Logging.csproj` gets PackageReadmeFile. One stale `Watch.Nesting` doc comment in `MethodLogger.cs` neutralized to `Pondhawk.Nesting`.

Phase 4 notes: split `Pondhawk.Watch.Tests` → `Pondhawk.Logging.Tests` (API/serializers/type-extensions + `CollectingSink`, `Logging/` flattened to root) and `Pondhawk.Logging.Watch.Tests` (sink/switch/events). Added `SerilogLoggerSourceTests`, `WatchLoggerTests` (switch-aware `IsEnabled`, `LogObject` skip vs emit, `ForContext` preservation), `WatchLoggerSourceTests`. Two stale `"watch.correlation"` test literals (and the 3 `"Watch.*"` event-construction literals in `WatchSinkTests`) had to be updated to the neutralized names — the only test changes needed, confirming no behavioral regression.

Phase 3 notes: `TypeExtensions` made **public** in Pondhawk.Logging (both logger sources must derive categories identically, and custom `ILoggerSource` impls benefit). `WatchLogger.IsEnabled` guards a blank category by delegating to the inner logger (`SwitchSource.Lookup` throws on blank). Exposed the `SwitchSource` via new `UseWatch`/`Watch` **out-param overloads** (CA1021 is off) so the root can share one instance with `WatchLoggerSource`. The switch-aware unit test is folded into Phase 4 (the test project is mid-migration).

Phase 2 notes: only `WatchSink.cs` needed repointing (`LogPropertyNames` + `PayloadType` via `using Pondhawk.Logging;`, and the `StartsWith("Watch.")` filter → `LogPropertyNames.Prefix`); `LogEvent.cs`'s only "PayloadType" mention was a comment. Solution `.slnx` and test projects still reference the old `Pondhawk.Watch` path — fixed in Phases 4–5.

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
