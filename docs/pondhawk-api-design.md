# Design Brief: Pondhawk.Api — ASP.NET web-app kit for Pondhawk Tools

Status: **draft / not started.** Reference implementation: [`kampilan/fabrica-thin` → `Fabrica.Endpoints`](https://github.com/kampilan/fabrica-thin/tree/master/Fabrica.Endpoints). First consumer / proving ground: `watch-server` (migrating off `AppCommon`).

**Principle:** this is the standard web kit **every Pondhawk app** uses — design for all consumers, using Fabrica.Endpoints as the reference for *what a Pondhawk web app needs*. "Judicious" applies to **implementation** (adapt to Pondhawk conventions, drop what doesn't fit), not **scope** (do not trim to one consumer's current usage). See memory `pondhawk-tools-build-for-all-consumers`.

## Goal

Fill the missing **web tier** in Pondhawk Tools. Today: `Pondhawk.Core` (domain/mediator/exceptions), `Pondhawk.Logging`, `Pondhawk.Rules`/`Rql`, `Pondhawk.Hosting` (generic host). `Pondhawk.Api` is the ASP.NET Core integration layer that lets a Pondhawk.Core domain be exposed over HTTP with endpoint modules, envelope→ProblemDetails mapping, gateway identity, diagnostics middleware, exception handling, and JSON conventions — replacing `AppCommon.Api` and modeled on `Fabrica.Endpoints`.

## Package & dependencies

**One package `Pondhawk.Api`** (mirrors Fabrica's single-project layout), `net10.0`, folders `Endpoints/ Filters/ Identity/ Middleware/ Exceptions/ Json/ Context/`.

- `FrameworkReference Microsoft.AspNetCore.App`
- `PackageReference Microsoft.IdentityModel.JsonWebTokens` (JWT — **replaces jose-jwt**)
- `PackageReference Microsoft.AspNetCore.OpenApi`
- `ProjectReference Pondhawk.Core`, `Pondhawk.Logging`, `Pondhawk.Rules` (Rules backs the validation behavior — replaces FluentValidation)

ASP.NET coupling stays isolated to this package; Core/Logging remain framework-light. (Revisit a `Pondhawk.Api.Identity` split only if a real need appears — Fabrica keeps it unified.)

## Locked implementation adaptations (Fabrica → Pondhawk)

| Fabrica uses | Pondhawk.Api uses |
|---|---|
| `Fabrica.Watch` (GetLogger/EnterMethod/Inspect/LogObject) | **Pondhawk.Logging** (same API surface; acquire via `ILoggerSource`/`Log.ForContext`) |
| `Autofac` (JSON config) | **Microsoft.Extensions.DI** (`services.Configure<JsonOptions>` + `AddSingleton`) |
| `jose-jwt` (HS256 encode/decode) | **Microsoft.IdentityModel.JsonWebTokens** (`JsonWebTokenHandler`, HS256 `SymmetricSecurityKey`) |
| `Fabrica.Core` `ErrorKind`/`ExternalException`/`ProblemDetail`/`Response` | **Pondhawk.Core** — all already present and shape-compatible |
| `Fabrica.Utilities.Container.ICorrelation`/`Correlation` | **new `Pondhawk.Api.Context` `IRequestContext`/`RequestContext`** (backbone — see below) |
| `Fabrica.Identity` `IClaimSet`/`ClaimSetModel`/`FabricaIdentity` | **new lightweight claim-set types in `Pondhawk.Api`** |
| `Fabrica.Utilities.Types.GetConciseName` | **Pondhawk.Logging.Utilities.TypeExtensions** (now public) |

## The one Pondhawk.Core enabler (additive, non-breaking)

Fabrica's `ResponseEndpointFilter`/`ExceptionHandler` inspect a boxed response via non-generic interfaces. Add to **Pondhawk.Core**:

```csharp
public interface IResponse
{
    bool Ok { get; }
    ErrorInfo? Error { get; }   // already carries Kind/ErrorCode/Explanation/Details
    object? Value { get; }
}
// Response<T> : IResponse  — Value via explicit interface impl returning the boxed T
```

`Pondhawk.Core.ProblemDetail` already has the exact shape Fabrica builds (`Type/Title/Detail/StatusCode/Instance/CorrelationId/Segments`), and `ErrorInfo` carries everything the ProblemDetail needs — so the filter/handler port almost verbatim.

**`ErrorKind` → HTTP status map** (Fabrica's, plus Pondhawk's extra `Remote`):
`None`→200 · `NotFound`→404 · `NotImplemented`→501 · `Predicate`→422 · `Conflict`→409 · `Concurrency`→410 · `BadRequest`→400 · `AuthenticationRequired`→401 · `NotAuthorized`→403 · `Functional`/`System`/`Unknown`→500 · **`Remote`→502**. Central + `virtual` so apps can override.

## Concern map (folder by folder)

### `Context/` — request context (the backbone)
`IRequestContext` (Pondhawk's `ICorrelation` equivalent), **lean to start**: `CorrelationId`, `Caller` (`ClaimsPrincipal?`), `CallerGatewayToken` (`string?`). No debug-probe/level fields in v1. Scoped, registered by `AddPondhawkApi()`, populated by the enrichment middleware. Used by the response filter (correlation id on ProblemDetails), exception handler, diagnostics middleware, and outbound token propagation. Correlation-id reuses Pondhawk.Logging's `CorrelationManager`/`Activity`.
> Consequence of "lean": the `DiagnosticsMonitorMiddleware` **probe-based level elevation** (which flips a context debug/level flag) is **deferred** — the monitor still does begin/end + elapsed timing; `RequestLoggingMiddleware` is unaffected (it gates on the Watch/Logging switch level, not a context flag). Add the debug/level fields later if the probe feature is wanted.

### `Endpoints/`
- `IEndpointModule` — `string BasePath => ""`, `void Configure(RouteGroupBuilder) {}`, `void AddRoutes(IEndpointRouteBuilder)`. (Richer than watch-server's current AppCommon shape; migration adds the two default members.)
- `EndpointExtensions` — `AddEndpointModules(services, params Assembly[])` (DI-registers each module as singleton) + `MapEndpointModules(builder, basePath, Action<RouteGroupBuilder>? root)` (resolves modules from DI, maps a group per `module.BasePath`, calls `Configure`+`AddRoutes`, **resilient try/catch per module** logging via Pondhawk.Logging).

### `Behaviors/` — mediator pipeline behaviors
- `ValidationBehavior<TRequest,TResponse> : IPipelineBehavior` — runs **Pondhawk.Rules** validation (`ValidationBuilder`/`IRuleSet`) against the request; on violations throws `FailedValidationException` (Pondhawk.Core), which the mediator envelopes as `ErrorKind.Predicate` → the response filter renders **422** with the violations in `Segments`. Replaces AppCommon's FluentValidation behavior. (Lives here rather than in Core/Rules because it bridges `Pondhawk.Core` mediator + `Pondhawk.Rules`; both stay standalone.)
- `LoggingBehavior<TRequest,TResponse> : IPipelineBehavior` — request/response logging via Pondhawk.Logging enriched from `IRequestContext` (correlation/user).

### `Filters/`
- `ResponseEndpointFilter : IEndpointFilter` — after the handler, if the result is `IResponse`: success→`Results.Json(value)`/`Results.Ok()`/`Results.Stream`; failure→`ProblemDetail` (`ErrorKind`→status) as `application/problem+json` with `CorrelationId` from `IRequestContext`. Handlers just return `Response<T>`.
- `ApiKeyEndpointFilter : IEndpointFilter` + `IApiKeyValidator`/`AbstractApiKeyValidator`/`SimpleApiKeyValidator` — `x-api-key` header, `CryptographicOperations.FixedTimeEquals`. Portable as-is.

### `Identity/` — gateway auth (one scheme, two inbound handlers)
Model: one scheme (`Pondhawk.GatewayToken`), pick the handler at registration.
- **Header mode** (`AddGatewayHeaderAuthentication`) — unsigned JSON claim-set in `X-Gateway-Identity`; deserialize + trust (network-boundary trust). No key/crypto.
- **Token mode** (`AddGatewayTokenAuthentication(base64Key)`) — HS256 JWT in `X-Gateway-Identity-Token`; validate signature + expiry via `JsonWebTokenHandler`.
- Server pieces: `GatewayHeaderAuthenticationHandler`, `GatewayTokenAuthenticationHandler`, `GatewayTokenAuthenticationSchemeOptions`, `AuthenticationBuilderExtensions` (`AddGatewayHeader`/`AddGatewayToken`), `ServiceCollectionExtensions`, `IdentityConstants` (header/scheme/policy/role names), `TokenAuthorizationFilter` (401 when unauthenticated), auth **policies** (`AllowPublic`, `RequiresAdminRole`).
- Client/propagation pieces (kept — apps call apps): `IGatewayTokenEncoder`/`GatewayTokenJwtEncoder` (HS256 via IdentityModel), `IGatewayTokenPayloadBuilder`/`ClaimGatewayTokenPayloadBuilder` (Claims→claim-set, standard claim map), `GatewayHeaderBuilderMiddleware`/`GatewayTokenBuilderMiddleware` (write the outbound header/token), `IAccessTokenSource` + `GatewayAmbientTokenSource`/`GatewayAccessTokenSource` + `GatewayTokenHttpHandler` (`DelegatingHandler` for outbound `HttpClient`).
- **JWT adaptation:** jose-jwt round-tripped the payload straight to/from a POCO; with IdentityModel we map our claim-set ↔ JWT claims explicitly (small, and lets us reuse `JwtRegisteredClaimNames`). HS256 symmetric only — no RSA/JWE.
- Claim-set types: lightweight `IClaimSet`/`ClaimSetModel` + a `ClaimsPrincipal` identity in Pondhawk.Api (STJ-serializable). **Minimal claim set (v1):** `UserId` (subject/identity), `UserName`, `FirstName`, `LastName`, `Email`, `Roles`. (Dropped Fabrica's Tenant/Flow/AltSubject/Picture/GivenName-FamilyName naming — mapped to First/Last.) `ClaimGatewayTokenPayloadBuilder`'s claim map covers exactly these standard claim types.

### `Middleware/`
- `RequestLoggingMiddleware` — trace-gated full HTTP request dump (route/query/headers/claims/pretty body), buffers+rewinds the body, **redacts** `Authorization`/gateway headers/cookies. Route through Pondhawk.Logging (`LogObject`/typed payloads).
- `DiagnosticsMonitorMiddleware` (+ `DiagnosticOptions`: probe header `X-Diagnostics-Probe`, `Level`, `Enrich`) — elevates log level for a correlation on a probe header; brackets the request with begin/end + elapsed timing.
- `DiagnosticsEnrichmentMiddleware` — sets `IRequestContext.Caller` (= `context.User`) and `CallerGatewayToken` (from the token header); runs `Enrich`.
- `MiddlewareExtensions` — `UseRequestLogging`/`UseDiagnosticsMonitor`/`UseDiagnosticsEnrichment` + `AddDiagnosticMiddleware`.

### `Exceptions/`
- `ExceptionHandler : IExceptionHandler` — exception→`ProblemDetail`. Derives `ErrorKind` from `ExternalException.Kind` (Pondhawk.Core), `JsonException`→`BadRequest`, else `System`; same status map; `application/problem+json`; `CorrelationId` from `IRequestContext`; 500s logged with context, others at Debug. Pairs with the response filter.

### `Json/`
- `CompactJsonTypeInfoResolver` — pure STJ; suppress "empty" members (empty collections, blank strings, zeros, min-value dates). Portable verbatim.
- `PascalJsonNamingPolicy` — no-op PascalCase policy. Verbatim.
- MS.DI JSON config extension — `AddPondhawkJson(Action<JsonSerializerOptions>)`: build options (Web defaults + resolver), `services.Configure<JsonOptions>` copy, register the instance. (Replaces Fabrica's Autofac variant one-to-one.)
- **Defer** `VtoJsonTypeInfoResolver` — it strips `Id`/`*Id` for `IEntity` types; Pondhawk has no persistence-entity marker. Add later as a generic extension point (predicate) if a consumer needs VTO shaping.

### Registration (target)
```csharp
services.AddPondhawkApi();                       // IRequestContext, ProblemDetails, filters wiring
services.AddMediator(typeof(Program).Assembly);  // Pondhawk.Core
services.AddPipelineBehavior(typeof(ValidationBehavior<,>));   // Pondhawk.Rules-backed
services.AddPipelineBehavior(typeof(LoggingBehavior<,>));
services.AddEndpointModules(typeof(Program).Assembly);
services.AddGatewayTokenAuthentication(base64Key); // or AddGatewayHeaderAuthentication()
services.AddPondhawkJson(o => { ... });
...
app.UseDiagnosticsEnrichment(); app.UseDiagnosticsMonitor(); app.UseRequestLogging();
app.MapEndpointModules("/api");
```

## Decisions

**Resolved:** one unified package · DI-activated endpoint modules · drop jose-jwt (Microsoft.IdentityModel, HS256) · drop Autofac (MS.DI) · support both header & token gateway modes · keep client/propagation identity (service-to-service is a platform need) · gateway token is HS256 symmetric (confirmed).

**Resolved (this round):**
1. **`IRequestContext`** — lean (`CorrelationId`/`Caller`/`CallerGatewayToken`); no debug-probe fields in v1 (probe level-elevation deferred).
2. **Claim-set** — minimal: `UserId`, `UserName`, `FirstName`, `LastName`, `Email`, `Roles`.
3. **Validation** — **Pondhawk.Rules** `ValidationBehavior` (not FluentValidation); throws `FailedValidationException` → 422.
4. **`RequestLoggingMiddleware`** — **full port** (body buffering + header redaction).

**Still open:**
- **VTO resolver** — deferred (no entity marker); add later as a predicate-based generic if a consumer needs VTO shaping.

## Migration plan (watch-server = proving ground)

1. Add the **`IResponse`** interface to `Pondhawk.Core` (+ tests).
2. Build **`Pondhawk.Api`** concern-by-concern (Context → Endpoints → Filters → Exceptions → Json → Identity → Middleware) with unit tests; publish to the feed.
3. Migrate **watch-server**: endpoint modules → Pondhawk.Api; mediator `using` swap + `ResponseEndpointFilter` (drop manual `Results.Ok`); gateway auth → Pondhawk.Api identity; middleware/exception-handler/JSON → Pondhawk.Api; `BaseEntity<T>` → tiny local class; remove all `AppCommon.*` refs.
4. Iterate the package from what the migration exposes (this validates the abstractions — v1 isn't "done" until watch-server is green on it).

## Risks
- **Single-consumer over-fitting** → design from the Fabrica reference (multi-app-proven), validate on watch-server.
- **ASP.NET framework/versioning** → isolated to `Pondhawk.Api`.
- **Gateway token wire-compat** → HS256 + `X-Gateway-Identity-Token` header + the claim map must match whatever mints tokens for the real gateway (confirm before cutover).
- **Feed availability** → `Pondhawk.Core`/`Logging` must be on a feed watch-server can consume.

## Verification gate
`Pondhawk.Core` + `Pondhawk.Api` build + unit tests green; watch-server migrates and its build + tests pass; all `AppCommon.*` references removed.
