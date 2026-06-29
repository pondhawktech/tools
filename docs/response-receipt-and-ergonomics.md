# Build brief: `Receipt` + `Response<T>` ergonomics (Pondhawk.Core)

**Repo:** `pondhawk-tools` · **Package:** `Pondhawk.Core` · **Target:** `net10.0`

Follow-up to `docs/mediator-response-envelope.md` (the `Response<T>` envelope, now shipped). This
adds the small pieces the entity-CRUD framework (which lives **in each app**, not here) needs to
compile against Core. **There is no non-generic `Response` — do not add one.** A command with no
entity to return uses `Response<Receipt>`.

## Why

The app-side CRUD framework reads results like this:

```csharp
var update = await Service.Repository.Eager(Eager, request.Uid, ct);
if (update.IsError) return update.AsError;     // propagate the failure, possibly to a different payload type
var entity = update.AsEntity;                  // the value, given success
...
return entity;                                  // implicit T -> Response<T>
...
return Receipt.One;                             // a delete/bulk command: no entity, just a tally
```

Core's `Response<T>` today has `Ok` / `Value` / `Error` / `Success` / `Failure` / `Match` /
`GetValueOrThrow` / implicit `T -> Response<T>`. It is missing `IsError`, `AsEntity`, `AsError`, the
cross-type error propagation, and the `Receipt` type. Add those.

## Current state

- `src/Pondhawk.Core/Mediator/Response.cs` — `readonly record struct Response<T>` (members above).
- `src/Pondhawk.Core/Exceptions/ErrorInfo.cs` — `sealed record ErrorInfo { Kind, ErrorCode, Explanation, Details }`.

## What to build

### 1. `Receipt` (namespace `Pondhawk.Mediator`, file `src/Pondhawk.Core/Mediator/Receipt.cs`)

The payload a mutation returns when it has no entity to hand back — proof it ran, plus a tally.

```csharp
public sealed record Receipt
{
    /// Number of entities affected. 1 for a single command, the real count for a bulk one,
    /// 0 = nothing changed (the canonical no-op signal).
    public int Affected { get; init; } = 1;

    public static Receipt One { get; } = new();
    public static Receipt Of(int affected) => new() { Affected = affected };
}
```

Do **not** add other fields (no Uid/Message/Timestamp/Outcome) — each must wait for a named consumer.

### 2. `Response<T>` ergonomics (extend `src/Pondhawk.Core/Mediator/Response.cs`)

Add, matching the existing style:

```csharp
public bool IsError => !Ok;                       // convenience inverse of Ok

public T AsEntity => GetValueOrThrow();           // value on success; throws if this is an error

public ErrorInfo AsError =>                       // the failure, to propagate; throws if this is a success
    Error ?? throw new InvalidOperationException("Response is not an error.");

// Cross-type error propagation: a failed Response<X> propagates to any Response<T>.
public static implicit operator Response<T>(ErrorInfo error) => Failure(error);
```

The implicit `ErrorInfo -> Response<T>` is what makes `return save.AsError;` work when `save` is a
`Response<Receipt>` (or any other payload type) and the method returns `Response<TEntity>`:
`AsError` yields the `ErrorInfo`, which converts to the target `Response<T>` carrying the same error.

> Note: `AsEntity` is the framework's name for the success-value accessor (alias of
> `GetValueOrThrow`). Keep both. The existing `implicit operator Response<T>(T value)` and the new
> `implicit operator Response<T>(ErrorInfo error)` are unambiguous because payload types `T` are
> never `ErrorInfo`.

## Acceptance criteria

- `dotnet build` and `dotnet test` green for `pondhawk-tools.slnx`.
- Tests:
  - `Receipt.One.Affected == 1`; `Receipt.Of(5).Affected == 5`.
  - `IsError == !Ok` for both a success and a failure response.
  - `AsEntity` returns the value on success and **throws** on a failure response.
  - `AsError` returns the `ErrorInfo` on failure and **throws** on a success response.
  - **Cross-type propagation:** take a failed `Response<int>`, `return failed.AsError;` into a method
    returning `Response<string>`, and assert the resulting `Response<string>` is an error carrying the
    *same* `ErrorInfo`.
  - implicit `ErrorInfo -> Response<T>` produces an error response with that `ErrorInfo`.
- XML doc comments on all new public members; keep the existing MIT header style; no new dependencies.

## Out of scope

No non-generic `Response`. No changes to `IMediator`/handlers/behaviors. The CRUD framework
(`BaseOneCommand`, `Repository`, endpoint bases, etc.) is **app-local** (`<Project>.Support`) and is
not built here.
