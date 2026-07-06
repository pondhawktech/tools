# Pondhawk.Api

The ASP.NET Core web-app kit for Pondhawk Tools (the successor to AppCommon.Api). It turns mediator
handlers into minimal-API endpoints: handlers return a `Response<T>` envelope and stay
transport-agnostic, while the kit renders that envelope to `Ok`/JSON on success or a ProblemDetails
(`application/problem+json`) on failure. It also supplies inbound gateway identity, diagnostics
middleware, exception handling, and JSON conventions.

Targets `net10.0` with a `FrameworkReference` to `Microsoft.AspNetCore.App`. Builds on
[`Pondhawk.Core`](../Pondhawk.Core/README.md) (mediator + `Response<T>`/`IResponse`),
[`Pondhawk.Logging`](../Pondhawk.Logging/README.md) (structured logging), and
[`Pondhawk.Rules`](../Pondhawk.Rules/README.md) (request validation). Namespace root: `Pondhawk.Api`.

## Concerns

- **Context** — `IRequestContext` (`CorrelationId`, `Caller` ClaimsPrincipal, `CallerGatewayToken`). The
  default `RequestContext` owns a stable correlation id (explicit > ambient `CorrelationManager.Current`
  > generated). Registered by `AddPondhawkApi()`.
- **Endpoints** — `IEndpointModule` (`BasePath`, `Configure(RouteGroupBuilder)`,
  `AddRoutes(IEndpointRouteBuilder)`): self-contained, DI-activated minimal-API route groups.
  `AddEndpointModules(assemblies)` discovers and registers them; `MapEndpointModules(basePath)` maps each
  module's group and is resilient — a module that throws while mapping is logged and skipped.
- **Filters** — `ResponseEndpointFilter` renders a handler's `Response<T>` (via the non-generic
  `IResponse`) to `Ok`/JSON/stream or a `ProblemDetail`. `ApiKeyEndpointFilter` +
  `IApiKeyValidator`/`SimpleApiKeyValidator` provide constant-time `x-api-key` checks.
- **Exceptions** — `ApiExceptionHandler` (`IExceptionHandler`) converts unhandled exceptions to a
  `ProblemDetail` (ExternalException keeps its kind, `JsonException` → 400, else System/500).
- **Json** — `CompactJsonTypeInfoResolver` (omits empty strings/collections/zeros/min-dates),
  `PascalJsonNamingPolicy`, and `AddPondhawkJson(configure)` (Microsoft.Extensions.DI — no Autofac).
- **Behaviors** — mediator pipeline behaviors: `ValidationBehavior<TReq,TResp>` runs Pondhawk.Rules
  `IRuleSet.TryValidate` over the request (violations → `FailedValidationException` → envelope
  `Predicate` → the filter renders 422); `LoggingBehavior<TReq,TResp>` does correlated request logging.
- **Identity** — inbound gateway authentication, one scheme with two handlers (see below).
- **Middleware** — `DiagnosticsEnrichmentMiddleware`, `DiagnosticsMonitorMiddleware`, and
  `RequestLoggingMiddleware`, wired with `UseDiagnosticsEnrichment`/`UseDiagnosticsMonitor`/
  `UseRequestLogging`.

### ErrorKind → HTTP status

The response filter and the exception handler share one mapping:

| ErrorKind | Status |
|-----------|--------|
| None | 200 |
| NotFound | 404 |
| NotImplemented | 501 |
| Predicate | 422 |
| Conflict | 409 |
| Concurrency | 410 |
| BadRequest | 400 |
| AuthenticationRequired | 401 |
| NotAuthorized | 403 |
| Remote | 502 |
| System / Functional / Unknown | 500 |

### Gateway identity (inbound only)

One authentication scheme (`IdentityConstants.Scheme`) with three handlers (register exactly one):

- **Token mode** — `AddGatewayTokenAuthentication(base64Key)` validates an HS256 JWT in the
  `X-Gateway-Identity-Token` header via `Microsoft.IdentityModel.JsonWebTokens`.
- **Header mode** — `AddGatewayHeaderAuthentication()` reads an unsigned JSON claim set from the
  `X-Gateway-Identity` header.
- **Development mode** — `AddGatewayDevelopmentAuthentication(ClaimSet)` authenticates every request as a
  fixed configured identity, with no gateway and no token, so authenticated code paths can be exercised
  locally. **Local development only** — never register it in a deployed configuration.

All three project a minimal `ClaimSet` (UserId, UserName, FirstName, LastName, Email, Roles) mapped to/from a
`ClaimsPrincipal` by `ClaimSetPrincipal`; `IGatewayTokenEncoder`/`GatewayTokenJwtEncoder` mint HS256
tokens. This package handles inbound authentication only — outbound/client token propagation is
deliberately out of scope.

## Quick Start

An endpoint module dispatches to the mediator; `ResponseEndpointFilter` renders the returned envelope:

```csharp
using Pondhawk.Api.Endpoints;
using Pondhawk.Api.Filters;
using Pondhawk.Mediator;

public sealed class OrderModule(IMediator mediator) : IEndpointModule
{
    public string BasePath => "/orders";

    public void Configure(RouteGroupBuilder group) =>
        group.AddEndpointFilter<ResponseEndpointFilter>();

    public void AddRoutes(IEndpointRouteBuilder app) =>
        app.MapGet("/{id:int}", (int id) => mediator.SendAsync(new GetOrder(id))); // returns Response<Order>
}
```

Register and map it in `Program.cs`:

```csharp
using Pondhawk.Api;
using Pondhawk.Api.Endpoints;
using Pondhawk.Api.Identity;
using Pondhawk.Api.Json;
using Pondhawk.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPondhawkApi();                                   // IHttpContextAccessor + scoped IRequestContext
builder.Services.AddPondhawkJson();                                 // compact resolver + Pascal naming
builder.Services.AddGatewayTokenAuthentication(signingKeyBase64);   // inbound HS256 gateway token
builder.Services.AddMediator(typeof(OrderModule).Assembly);         // Pondhawk.Core mediator
builder.Services.AddEndpointModules(typeof(OrderModule).Assembly);  // discover IEndpointModules

var app = builder.Build();

app.UseDiagnosticsMonitor();      // register EARLY: whole-pipeline begin/end + elapsed
app.UseAuthentication();
app.UseDiagnosticsEnrichment();   // after auth: populate IRequestContext (correlation/caller/token)
app.UseRequestLogging();          // deep: full request dump when the diagnostics category is debug-enabled

app.MapEndpointModules("/api");
app.Run();
```

## Documentation

See [CLAUDE.md](CLAUDE.md) for the full concern-by-concern reference, the ProblemDetails/ErrorKind
mapping, the gateway-auth model, the two-diagnostics-middleware pipeline rule, and the project layout.
