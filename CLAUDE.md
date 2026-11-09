# Order Processing Platform: working instructions

These instructions apply to every task in this repository, for every agent and subagent.
Read `docs/REQUIREMENTS.md` before the first change of a session. It says what we build and why.

## 1. Before writing any code

1. **Read the requirement.** Find the functional or non-functional requirement in `docs/REQUIREMENTS.md` that the change serves. If none applies, stop and ask.
2. **Check what already exists.** Search the solution (Grep and Glob) for types, interfaces, extension methods, helpers, test builders and fakes that already do the job or most of it. Reuse or extend them. Never create a parallel version of something that exists.
3. **Find the owner.** Decide which project owns the change (see section 3). A change that needs to touch another module's internals is a design problem: raise it, do not work around it.
4. **Plan the smallest change.** List the files you will touch before you edit them. Write the least code that meets the requirement.

## 2. Principles

### SOLID, applied to this repository
- **Single responsibility:** one reason to change per class. A pipeline step does one external action and its undo. An endpoint maps HTTP to a use case and back; it holds no business rules.
- **Open/closed:** extend through the existing extension points: a new pipeline step, a new pipeline version, a new `IPricingRule`, a new gateway implementation. Do not add `if`/`switch` chains on types or providers.
- **Liskov:** every implementation of an interface honours the full contract, including cancellation, idempotency and error mapping. A fake behaves like the real system, including its failures.
- **Interface segregation:** small, focused interfaces (`IInventoryGateway`, not `IExternalSystems`). Consumers depend only on what they call.
- **Dependency inversion:** modules depend on `.Contracts` abstractions. Concrete classes are wired only in the composition roots (`OrderPlatform.Api`, `OrderPlatform.Worker`).

### Clean code
- Names say what a thing is or does, in the language of the domain (Order, Reservation, Authorization, Capture). No abbreviations.
- Small methods with one level of abstraction. Guard clauses instead of nested `if`.
- No magic numbers or strings: use constants, options or enums.
- Comments explain *why*, never *what*. No commented-out code. No dead code.
- No premature generalisation: build what a requirement needs. The saga engine is a deliberate, documented exception (see ADR 0002).

### Reusable code
- Shared concepts live in `BuildingBlocks` (Money, Result, IAuditable, ICurrentUser, SystemUser). Add to it only when a second module needs the same thing.
- Rule of three: duplicate once if you must, extract on the third occurrence.
- Test helpers (builders, fakes, fixtures) are shared through a test utilities project, not copied between test projects.

## 3. Architecture rules

These rules are enforced by `tests/Architecture.Tests`. A change that breaks them is not done.

| Project | Owns | May reference |
|---|---|---|
| `OrderPlatform.Api` | Endpoints, auth, ProblemDetails, health, SignalR hub, outbox dispatcher, DI wiring | Everything (composition root) |
| `OrderPlatform.Worker` | Background service that runs due pipeline runs | Everything (composition root) |
| `BuildingBlocks` | Money, Result, IAuditable, ICurrentUser, SystemUser | Nothing |
| `ProcessEngine` | Step contract, base step, runner, registry, run entities (schema `process`) | BuildingBlocks only. It knows nothing about orders |
| `Modules.Ordering` | Order aggregate, status rules, status history, the 7 steps, pipeline V1, use cases (schema `ordering`) | ProcessEngine, BuildingBlocks, any `*.Contracts` |
| `Modules.Catalog` (+ `.Contracts`) | Products, prices, tax categories, shipping rates (schema `catalog`) | BuildingBlocks |
| `Modules.Pricing` (+ `.Contracts`) | Pricing service and `IPricingRule` implementations | BuildingBlocks, Catalog.Contracts |
| `Integrations.*` (+ `.Contracts`) | Anti-corruption layer for inventory, payment, shipping: gateway interfaces, clients, error mapping | BuildingBlocks |
| `FakeExternalSystems` | In-memory fakes of the external systems with failure modes | `Integrations.*.Contracts`. Registered only in Development and tests |
| `Persistence` | AppDbContext, audit interceptor, conventions, migrations, seed | Modules (entity configurations only) |

Further rules:
- A module never references another module's implementation project, only its `.Contracts`.
- Each module owns its entity configurations and its schema. There is one `AppDbContext` that applies them.
- A pipeline step only changes the run context. It never calls `SaveChanges`. The runner saves context, step status, attempts, order status, history and outbox in **one transaction**.
- Every call to an external system sends an idempotency key `{runId}:{stepKey}:{Execute|Compensate}`.
- The pipeline definition lives in code. A change to the steps is a new pipeline version; never edit a released version.

## 4. .NET conventions

- .NET 10, latest C#, `Nullable` enabled, warnings as errors, file-scoped namespaces, central package management (`Directory.Packages.props`).
- Async all the way. Every async method takes a `CancellationToken` as its last parameter and passes it on. Never use `.Result` or `.Wait()`.
- Time comes from `TimeProvider`. Never call `DateTime.Now` or `DateTime.UtcNow`.
- Money is always the `Money` type, never a raw `decimal`. Rounding rules live in one place.
- Enums are stored as strings in the database.
- Entities have private setters and behaviour methods; invariants are enforced inside the aggregate.
- Minimal APIs. Expected failures return `Result` and map to ProblemDetails. Exceptions are for the unexpected only.
- Configuration through the Options pattern with validation on start.
- Structured logging with `ILogger<T>` and message templates (no string interpolation in log calls).
- Auditing through `IAuditable` and the SaveChanges interceptor. The worker writes as the seeded system user.

## 5. Testing

- **Every change ships with unit tests.** No exceptions for "small" changes.
- xUnit, NSubstitute for mocks, hand-written fakes for external systems, `FakeTimeProvider` for anything time based.
- Test names: `Method_Scenario_ExpectedResult`. Arrange, Act, Assert, one behaviour per test.
- Cover the happy path, every failure branch and the edge cases (empty, zero, boundaries, cancellation).
- Integration tests use a real SQL Server through Testcontainers, never the EF InMemory provider.
- Tests never depend on each other or on execution order.

## 6. Workflow for every change

1. Plan: requirement, owner project, files to touch, existing code to reuse.
2. Implement the smallest change.
3. Run the **test-writer** subagent for the change. It adds or updates the unit tests.
4. Run `dotnet build` and `dotnet test`. Both must pass.
5. Run the **change-reviewer** subagent. Fix every finding it marks as blocking.
6. Report in a few lines: what changed, which tests were added, the build and test result, anything deferred.

## 7. Writing rules

- All code, comments and documents are in English.
- Never use the em dash character in code, comments or documents. Use a colon, a comma or a new sentence.
- Keep documents in `docs/` up to date with the change in the same commit.

## 8. Definition of Done

- The change serves a requirement in `docs/REQUIREMENTS.md`.
- Existing code was reused where possible; no duplicates were introduced.
- Unit tests were added and pass; the full test suite passes.
- Architecture tests pass.
- The change-reviewer subagent reports no blocking findings.
- Related documents are updated.
