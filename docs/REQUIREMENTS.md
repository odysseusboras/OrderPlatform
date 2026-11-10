# Order Processing Platform: requirements

## 1. Purpose

This repository is the answer to a Solution Architect assessment: design the skeleton of a modular **Order Processing Platform** in .NET.

The goal is not a complete application. The goal is an **implementation-ready skeleton** that a delivery team can take over and continue. It must show:
- how the solution is structured and where the module boundaries are,
- which decisions were made and why,
- how the architecture addresses the functional and non-functional requirements,
- a working slice that proves the design runs.

Reviewers grade architectural thinking, decomposition, clarity of the artifacts, the ability to justify decisions, and judgement in balancing breadth and depth. AI use is expected; every artifact must be explainable and defensible, including where AI suggestions were rejected.

## 2. Functional requirements

| Id | Requirement (from the brief) | How this submission delivers it |
|---|---|---|
| FR1 | Create an order containing one or more products | Built. `POST /api/orders` checks the products against the catalog (unknown, duplicated or mixed-currency products: `400`), saves the order as Pending with its items and starts the fulfilment pipeline in the same transaction. Without waiting it returns `202` and the worker runs the pipeline. With `Prefer: wait=<seconds>` (RFC 7240, capped by configuration) the API runs ReserveStock and CalculatePrice itself and returns `201` with the order Priced, or Rejected when an item is out of stock or cannot be priced |
| FR2 | Retrieve an existing order | Built. `GET /api/orders/{id}` (owner only; another customer's order is a 404) with items, price, status and status history; `GET /api/orders` lists the caller's orders, paged |
| FR3 | Cancel an order when permitted | Built. `POST /api/orders/{id}/cancel` runs the saga backward inside the request: every completed step is undone in reverse order (parcel recalled, payment refunded, shipment booking cancelled, stock released, as far as the order got) and the order is returned Cancelled (`200`). Permitted until the order is delivered (the pivot); after that, or for an order already cancelled or rejected: `409` |
| FR4 | Track the lifecycle of an order | Built. The order follows its run: Pending, Priced, Confirmed, Paid, Shipped, Delivered, or Rejected / Cancelled / Failed. Every change is kept in the status history with its reason. `POST /api/orders/{id}/delivered` confirms the delivery. Pushing changes to clients (SignalR) is designed, not built |
| FR5 | Validate inventory availability before accepting an order | Built. ReserveStock is the first step of the pipeline, before the price is calculated. The reservation is the availability check: a read-only check could be outdated a moment later. Out of stock rejects the order at once; if a later step fails, the reservation is released. The order is accepted (Confirmed) only after stock is reserved and the payment is authorized |
| FR6 | Calculate pricing, including taxes and additional charges | Built. `IPricingService` prices the order items at their current catalog price through pricing rules (VAT per line, then a shipping charge, free above a threshold). Step CalculatePrice stores the price snapshot on the order. `GET /api/offers` lists what is on sale, paged |
| FR7 | Integrate with external inventory, payment and shipping systems | Built up to the boundary. Each system has a gateway interface in a `.Contracts` project (the anti-corruption layer) and the pipeline steps call them with idempotency keys. Behind the interfaces are in-memory fakes (Development and tests; the inventory fake has configurable failures) and placeholder clients that accept every call. The real HTTP clients are the next milestones |

## 3. Non-functional requirements

"Built" means it is in the code and tested. "Designed" means the architecture has a place for it and it is not implemented.

| Requirement | Built | Designed, not built |
|---|---|---|
| Modularity | Modular monolith; each module owns its entities, mapping and schema and exposes only a `.Contracts` project; `Architecture.Tests` fails the build on a forbidden reference | Extracting a module into its own service along its contract |
| Maintainability | Generic saga engine separated from order logic; pipeline defined in code and validated at startup; expected failures as `Result` with stable error codes; one place per concern | ADRs and a team guide |
| Testability | Gateways behind interfaces; fakes; `TimeProvider`; module and engine tests on SQLite; model tests for the SQL Server specifics (temporal tables, schemas, migrations in sync) | Tests against a real SQL Server (Testcontainers) |
| Scalability | Stateless API; separate worker host; runs are locked while processed, with optimistic concurrency as a second guard; every list is paged in the database | Several workers taking batches without contention (`READPAST` or a queue) |
| Auditability | Audit fields on every entity; every table is a temporal table, so every earlier version of a row can be queried | History retention for the high-churn run tables |
| Reliability | Orchestrated saga with compensation; state saved after every step; retries with exponential backoff; idempotency keys on every external call; a shutdown interrupts a step without losing a retry; RequiresAttention instead of silent failure | Idempotency-Key on `POST /api/orders`; outbox for events; back-office actions for runs that need attention |
| Security | Owner-only order access; the `Carrier` role on the delivery confirmation; payment tokens only, never card data; rate limiting per client (429 with Retry-After); maximum page size; one error format with a trace id and no internal details | Token authentication with roles (Customer, BackOffice, Carrier). Until then Development and tests sign in with the `X-User-Id` header and the API refuses to start in any other environment; the role comes from the `X-User-Role` demo header until then; signed carrier webhooks |
| Observability | Structured logs with message templates for every step, compensation and escalation; trace id in every error response | OpenTelemetry traces and metrics; alerts on stuck runs |
| Deployment | Two hosts (API, Worker); EF Core migrations (applied on start in Development only) | Docker images, CI pipeline, migration bundle, cloud target |
| Extensibility | New step means a new pipeline version; new charge or promotion means a new `IPricingRule`; new provider means a new gateway implementation; optional steps through `ShouldRun`; a module follows its runs through `IPipelineObserver` | |

## 4. Core design

### Order fulfilment pipeline (saga)

| # | Step | Type | Compensation |
|---|---|---|---|
| 1 | ReserveStock | Compensatable | Release reservation. Skipped for orders with digital products only |
| 2 | CalculatePrice | Compensatable | None needed (snapshot kept for audit) |
| 3 | AuthorizePayment | Compensatable | Void authorization |
| 4 | PrepareShipment | Compensatable | Cancel shipment booking |
| 5 | CapturePayment | Compensatable | Refund the captured amount |
| 6 | DispatchShipment | Compensatable | Recall the parcel |
| 7 | AwaitDelivery | **Pivot**, OnDemand | None. Triggered by carrier webhook or back-office; then the stock reservation becomes a sale. Timeout leads to RequiresAttention |

Rules:
- On a fatal error before the pivot, undo every succeeded step in reverse order. Skipped steps and the failed step itself are never undone.
- A fatal error after the pivot, retries that run out, or an undo that fails stop the run as RequiresAttention.
- Retries use exponential backoff per step (base delay doubled per attempt, up to a maximum). An exception from a step counts as a retryable error.
- A shutdown during a step is Interrupted: the step runs again and the attempt does not count towards the retry limit.
- Step results: Success, RetryableError, FatalError, Waiting, Interrupted (shutdown, not a failure).
- A run is pinned to the pipeline version it started with.
- Each call to the executor makes one move (run a step, undo a step, skip a step, or complete) and the caller saves after every move.
- The order follows its run: after every move an observer (`IPipelineObserver`) updates the order in the same transaction. It becomes Priced when the price exists, and Cancelled, Rejected or Failed as soon as the run starts undoing.
- Inline start: the API may run the first steps inside the request (`IPipelineRunner.RunUntilAsync`) within a time budget. The run is saved locked by the API in the same transaction as the order, so the worker cannot run it at the same time; the lock is released at the end and expires if the API crashes. A failed step is undone by the worker; a customer cancellation is undone inline (`RunCancellationAsync`) under the same lock.

### Statuses
- **Order:** Pending, Priced, Confirmed (accepted), Paid, Shipped, Delivered, Cancelled, Rejected, Failed.
- **Run:** Running, Waiting, Compensating, Completed, Compensated, RequiresAttention.

### Live updates (designed, not built)
The worker writes an `OrderStatusChanged` message to an outbox in the same transaction as each step, and a dispatcher pushes it to the SignalR group `order-{id}`. `GET /api/orders/{id}` remains the source of truth.

## 5. Scope of this submission

**Built and tested:**
- Solution structure and module boundaries, enforced by architecture tests.
- Persistence: one schema per module, audit interceptor, system user, enums as strings, temporal tables, seed data (products, prices, VAT categories), migrations.
- Process engine: validated pipeline definitions, executor (one move per call), compensation, retries with backoff, ShouldRun, OnDemand steps with timeout, inline runner with lock, background worker.
- The 7 steps of pipeline V1, with pricing rules for VAT and shipping.
- Orders API: create (optionally waiting for the price), get, list, cancel (saga backward), confirm delivery. Offers API.
- Fake inventory, payment and shipping systems; failure scenarios are configured in the fakes (section `FakeExternalSystems`), never seeded into the real catalog.
- Two hosts: API (runs the worker too in Development, so one process is enough for a demo) and Worker.

**Not built (see the "Designed" column above):**
- Real inventory, payment and shipping clients.
- Token authentication, and roles beyond `Carrier`.
- SignalR live updates and the outbox.
- Idempotency-Key on create, back-office actions for stuck runs.
- Docker, CI, telemetry.
- Promotions, returns, partial shipments, UI.

## 6. Assumptions

- About 10,000 orders per day, peaks of 10x.
- One country, one currency (EUR), VAT per product category.
- External systems are reachable over HTTPS, may be slow or unavailable for minutes, and accept idempotency keys.
- One delivery per order. Card authorizations stay valid for 7 days.

## 7. Deliverables

- The source code and tests described in section 5, and `src/Hosts/OrderPlatform.Api/OrderPlatform.Api.http` as the demo script.
- `docs/process-engine.docx`: why a saga, why a polling worker instead of a message bus or Hangfire, how the engine works, build or buy, known gaps.
- `docs/technical-wiki.docx`: the architecture, the API (paging, rate limiting, errors), data, logging and the trace id, configuration, how to run it.
- `docs/security.docx`: what protects the system today, what is stubbed, the threats and the plan to close each gap.
- `docs/milestones.docx`: what is delivered and the plan from here to the first real order and beyond.
- `README.md`: what this prototype is, the main decision, how to run it, what is real and what is stubbed, how AI was used.
- Still to write: `docs/adr/`.

## 8. Glossary

- **Pipeline / saga:** the ordered list of steps that fulfils an order.
- **Run:** one execution of a pipeline for one order.
- **Compensation:** the undo action of a step.
- **Pivot:** the step after which no automatic undo is possible (AwaitDelivery: a delivered order is final).
- **Gateway:** the interface through which the platform talks to an external system.
- **RequiresAttention:** a run that needs a human decision.
