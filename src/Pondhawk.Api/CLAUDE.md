# Pondhawk.Api - AI Development Guide

## Overview

Pondhawk.Api is the **ASP.NET Core web-app kit** for Pondhawk Tools (the successor to AppCommon.Api). It
lets mediator handlers back minimal-API endpoints: a handler returns a `Response<T>` envelope and stays
transport-agnostic, while the kit renders that envelope to `Ok`/JSON on success or a ProblemDetails
(`application/problem+json`) on failure. On top of that it provides inbound gateway identity,
diagnostics middleware, an exception handler, and JSON conventions.

Targets `net10.0` with a `FrameworkReference` to `Microsoft.AspNetCore.App`. Builds on
[`Pondhawk.Core`](../Pondhawk.Core/CLAUDE.md) (mediator + `Response<T>` / the non-generic `IResponse`),
[`Pondhawk.Logging`](../Pondhawk.Logging/CLAUDE.md) (structured logging), and
[`Pondhawk.Rules`](../Pondhawk.Rules/README.md) (request validation). Its only third-party
PackageReference is `Microsoft.IdentityModel.JsonWebTokens`. Namespace root: `Pondhawk.Api`.

The design point: **handlers never touch HTTP.** They return `Response<T>`; the response filter and the
exception handler are the single seam that turns success/failure into status codes, so the same
`ErrorKind → HTTP status` policy applies everywhere.

---

## Concern-by-Concern Reference

### Context (`Pondhawk.Api.Context`)

- **`IRequestContext`** — per-request ambient state: `CorrelationId`, `Caller` (`ClaimsPrincipal`), and
  `CallerGatewayToken`.
- **`RequestContext`** — the default implementation; owns a stable correlation id resolved as
  *explicit > ambient `CorrelationManager.Current` > freshly generated*. Registered (scoped) by
  `AddPondhawkApi()` along with `IHttpContextAccessor`.

### Endpoints (`Pondhawk.Api.Endpoints`)

- **`IEndpointModule`** — a self-contained group of minimal-API routes: `BasePath` (route prefix),
  `Configure(RouteGroupBuilder)` (shared filters/auth/metadata, optional), and
  `AddRoutes(IEndpointRouteBuilder)`.
- **`AddEndpointModules(assemblies)`** — discovers concrete modules and registers each as a singleton, so
  modules can take constructor dependencies (e.g. `IMediator`).
- **`MapEndpointModules(basePath, configureRoot?)`** — maps every registered module under `basePath`,
  giving each its own group at its `BasePath`. **Resilient:** a module that throws while mapping is logged
  and skipped so one bad module cannot abort startup.

### Filters (`Pondhawk.Api.Filters`)

- **`ResponseEndpointFilter`** — the core renderer. It inspects the handler result; if it is an
  `IResponse` (the non-generic view `Response<T>` implements), it renders: on success a `Stream` becomes
  `Results.Stream`, a null value becomes `Results.Ok()`, otherwise `Results.Json(value)`; on failure it
  builds a `ProblemDetail` with `application/problem+json` and the mapped status. `MapToStatus` is
  `virtual` (defaults to `ErrorStatusMap.ToStatus`) so you can override the mapping. Attach it per group
  with `group.AddEndpointFilter<ResponseEndpointFilter>()`.
- **`ApiKeyEndpointFilter`** with **`IApiKeyValidator`** / **`AbstractApiKeyValidator`** /
  **`SimpleApiKeyValidator`** — constant-time `x-api-key` validation.

### Exceptions (`Pondhawk.Api.Exceptions`)

- **`ApiExceptionHandler`** — an `IExceptionHandler` for anything that escapes the filter: an
  `ExternalException` keeps its `ErrorKind`, a `JsonException` maps to 400, everything else becomes
  System/500 — all rendered as a `ProblemDetail`.
- **`ErrorStatusMap`** — the internal `ErrorKind → HTTP status` mapping shared by the filter and the
  handler (see the table below).

### Json (`Pondhawk.Api.Json`)

- **`CompactJsonTypeInfoResolver`** — omits empty strings/collections, zeros, and min-dates to keep
  payloads compact.
- **`PascalJsonNamingPolicy`** — PascalCase property names.
- **`AddPondhawkJson(configure?)`** — builds `JsonSerializerOptions` from Web defaults + the compact
  resolver, lets the caller customize, applies it to minimal-API `JsonOptions`, and registers the options
  instance as a singleton so filters/handlers can resolve it. Pure Microsoft.Extensions.DI — no Autofac.

### Behaviors (`Pondhawk.Api.Behaviors`)

Mediator `IPipelineBehavior<TReq,TResp>` implementations (registered with
`AddPipelineBehavior(typeof(...<,>))`):

- **`ValidationBehavior<TReq,TResp>`** — runs Pondhawk.Rules `IRuleSet.TryValidate` over the request;
  violations become a `FailedValidationException`, which the mediator envelopes as `ErrorKind.Predicate`,
  which the response filter renders as **422**.
- **`LoggingBehavior<TReq,TResp>`** — correlated request logging via Pondhawk.Logging.

### Identity (`Pondhawk.Api.Identity`)

Inbound gateway authentication — see "Gateway auth model" below.

### Middleware (`Pondhawk.Api.Middleware`)

`DiagnosticsEnrichmentMiddleware`, `DiagnosticsMonitorMiddleware`, and `RequestLoggingMiddleware`, wired
with `UseDiagnosticsEnrichment` / `UseDiagnosticsMonitor` / `UseRequestLogging` — see "Diagnostics
middleware: position matters" below.

### Root (`Pondhawk.Api`)

- **`AddPondhawkApi()`** — registers `IHttpContextAccessor` and a scoped `IRequestContext`. Everything
  else (endpoint modules, gateway auth, JSON, pipeline behaviors) is opted into by its own extension.

---

## ProblemDetails / ErrorKind Mapping

The filter and the exception handler render every failure through one policy (this mapping deliberately
lives in the ASP.NET layer, not in Pondhawk.Core):

| ErrorKind | HTTP status |
|-----------|-------------|
| None | 200 OK |
| NotFound | 404 Not Found |
| NotImplemented | 501 Not Implemented |
| Predicate | 422 Unprocessable Entity |
| Conflict | 409 Conflict |
| Concurrency | 410 Gone |
| BadRequest | 400 Bad Request |
| AuthenticationRequired | 401 Unauthorized |
| NotAuthorized | 403 Forbidden |
| Remote | 502 Bad Gateway |
| System / Functional / Unknown | 500 Internal Server Error |

A failure `ProblemDetail` carries `Type` (the kind), `Title` (error code), `Detail` (explanation),
`StatusCode`, `Instance` (request path), `CorrelationId` (from `IRequestContext`), and `Segments`
(structured error details).

---

## Gateway Auth Model — one scheme, three modes (inbound only)

There is **one authentication scheme** (`IdentityConstants.Scheme`) with **three handlers** (register exactly one):

- **Token mode** — `AddGatewayTokenAuthentication(signingKeyBase64)` registers a `GatewayTokenJwtEncoder`
  from the base64 HS256 key and the `GatewayTokenAuthenticationHandler`, which validates an HS256 JWT in
  the **`X-Gateway-Identity-Token`** header via `Microsoft.IdentityModel.JsonWebTokens`.
- **Header mode** — `AddGatewayHeaderAuthentication()` registers the `GatewayHeaderAuthenticationHandler`,
  which reads an **unsigned** JSON claim set from the **`X-Gateway-Identity`** header. No signing key.
- **Development mode** — `AddGatewayDevelopmentAuthentication(ClaimSet)` registers the
  `GatewayDevelopmentAuthenticationHandler`, which authenticates **every** request as a fixed configured
  identity, with no gateway and no token. Its sole purpose is to exercise an app's authenticated code
  paths (a "current user" endpoint, role-gated routes) **locally, off the gateway**. **Local development
  only** — it authenticates unconditionally, so never register it in a deployed configuration. (Behind the
  gateway use token/header mode; a standalone deployment that needs no user simply leaves auth off.)

All three project a minimal **`ClaimSet`** (UserId, UserName, FirstName, LastName, Email, Roles), mapped
to/from a `ClaimsPrincipal` by **`ClaimSetPrincipal`**. **`IGatewayTokenEncoder`** /
**`GatewayTokenJwtEncoder`** mint HS256 tokens for the token mode.

**This package handles inbound authentication only.** There is deliberately no outbound/client token
propagation — no client `HttpMessageHandler`, access-token source, or propagation middleware. That is an
HTTP-client concern, not an API concern, and would belong to a future `Pondhawk.Identity`. Do not add
outbound propagation here.

---

## Diagnostics Middleware — position matters

The three middlewares are **complementary by pipeline position**, not redundant:

- **`DiagnosticsMonitorMiddleware`** (`UseDiagnosticsMonitor`) — register **EARLY**. Brackets the whole
  pipeline with begin/end + elapsed timing, so it also catches requests short-circuited deeper down.
- **`DiagnosticsEnrichmentMiddleware`** (`UseDiagnosticsEnrichment`) — register **after authentication**.
  Populates `IRequestContext` with correlation id, caller, and gateway token.
- **`RequestLoggingMiddleware`** (`UseRequestLogging`) — register **DEEP**. Dumps the full request
  (method, path, query, redacted headers, buffered body) only when the `Pondhawk.Diagnostics.Http`
  category is debug-enabled.

Typical order: `UseDiagnosticsMonitor()` → `UseAuthentication()` → `UseDiagnosticsEnrichment()` →
`UseRequestLogging()`.

---

## Quick Start

```csharp
using Pondhawk.Api;
using Pondhawk.Api.Endpoints;
using Pondhawk.Api.Filters;
using Pondhawk.Api.Identity;
using Pondhawk.Api.Json;
using Pondhawk.Api.Middleware;
using Pondhawk.Mediator;

public sealed class OrderModule(IMediator mediator) : IEndpointModule
{
    public string BasePath => "/orders";

    public void Configure(RouteGroupBuilder group) =>
        group.AddEndpointFilter<ResponseEndpointFilter>();

    public void AddRoutes(IEndpointRouteBuilder app) =>
        app.MapGet("/{id:int}", (int id) => mediator.SendAsync(new GetOrder(id))); // Response<Order>
}

// Program.cs
builder.Services.AddPondhawkApi();
builder.Services.AddPondhawkJson();
builder.Services.AddGatewayTokenAuthentication(signingKeyBase64);
builder.Services.AddMediator(typeof(OrderModule).Assembly);
builder.Services.AddEndpointModules(typeof(OrderModule).Assembly);

var app = builder.Build();
app.UseDiagnosticsMonitor();
app.UseAuthentication();
app.UseDiagnosticsEnrichment();
app.UseRequestLogging();
app.MapEndpointModules("/api");
app.Run();
```

---

## Project Structure

```
src/Pondhawk.Api/
  ServiceCollectionExtensions.cs          # AddPondhawkApi() — IHttpContextAccessor + scoped IRequestContext
  GlobalUsings.cs

  Context/
    IRequestContext.cs                    # CorrelationId, Caller, CallerGatewayToken
    RequestContext.cs                     # default; stable correlation id (explicit > ambient > generated)

  Endpoints/
    IEndpointModule.cs                    # BasePath / Configure / AddRoutes
    EndpointExtensions.cs                 # AddEndpointModules, MapEndpointModules (resilient)

  Filters/
    ResponseEndpointFilter.cs             # Response<T> (IResponse) -> Ok/JSON/stream or ProblemDetail
    ApiKeyEndpointFilter.cs               # x-api-key gate
    IApiKeyValidator.cs                   # validator abstraction
    AbstractApiKeyValidator.cs            # constant-time comparison base
    SimpleApiKeyValidator.cs              # single-key validator

  Exceptions/
    ApiExceptionHandler.cs                # IExceptionHandler -> ProblemDetail
    ErrorStatusMap.cs                     # ErrorKind -> HTTP status (shared with the filter)

  Json/
    CompactJsonTypeInfoResolver.cs        # omits empty strings/collections/zeros/min-dates
    PascalJsonNamingPolicy.cs             # PascalCase property names
    JsonServiceCollectionExtensions.cs    # AddPondhawkJson(configure)

  Behaviors/
    ValidationBehavior.cs                 # IRuleSet.TryValidate -> FailedValidationException -> 422
    LoggingBehavior.cs                    # correlated request logging

  Identity/
    IdentityConstants.cs                  # the single scheme name
    IdentityServiceCollectionExtensions.cs# AddGatewayTokenAuthentication / AddGatewayHeaderAuthentication
    AuthenticationBuilderExtensions.cs    # AddGatewayToken / AddGatewayHeader (one scheme, two handlers)
    GatewayAuthenticationSchemeOptions.cs # scheme options
    GatewayTokenAuthenticationHandler.cs  # token mode: HS256 JWT in X-Gateway-Identity-Token
    GatewayHeaderAuthenticationHandler.cs # header mode: unsigned JSON in X-Gateway-Identity
    ClaimSet.cs                           # UserId, UserName, FirstName, LastName, Email, Roles
    ClaimSetPrincipal.cs                  # ClaimSet <-> ClaimsPrincipal mapping
    IGatewayTokenEncoder.cs               # token encoder abstraction
    GatewayTokenJwtEncoder.cs             # HS256 JWT encoder

  Middleware/
    DiagnosticsMonitorMiddleware.cs       # register EARLY: whole-pipeline begin/end + elapsed
    DiagnosticsEnrichmentMiddleware.cs    # after auth: populate IRequestContext
    RequestLoggingMiddleware.cs           # register DEEP: full request dump (debug-gated)
    MiddlewareExtensions.cs               # UseDiagnosticsMonitor/Enrichment, UseRequestLogging
```

> There is intentionally **no** outbound/client token-propagation type here (no propagation
> `HttpMessageHandler`, no access-token source, no client builder middleware). If you are looking for
> one, it does not belong in this package.

## Common Mistakes

- Don't render HTTP results in handlers — return `Response<T>` and let `ResponseEndpointFilter` map it.
- Don't reorder the diagnostics middlewares: monitor EARLY, enrichment AFTER auth, request-logging DEEP.
- Don't register two schemes for gateway auth — it is one scheme (`IdentityConstants.Scheme`) with two
  handlers; pick token mode or header mode.
- Don't add outbound token propagation to this package.
