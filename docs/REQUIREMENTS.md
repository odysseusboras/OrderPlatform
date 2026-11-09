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

| Id | Requirement (from the brief) | How M1 delivers it |
|---|---|---|
| FR1 | Create an order containing one or more products | `POST /orders` saves the order as Pending with its items and starts the fulfilment pipeline in the same transaction; returns `202 Accepted` with the order id |
| FR2 | Retrieve an existing order | `GET /orders/{id}` (owner or back-office only) with items, totals, status and status history; `GET /orders` lists the caller's orders |
| FR3 | Cancel an order when permitted | `POST /orders/{id}/cancel` is allowed until the pivot step (CapturePayment) has succeeded; the runner compensates the completed steps; after the pivot it returns 409 |
| FR4 | Track the lifecycle of an order | Order status with full status history; every change is pushed to the client through SignalR |
| FR5 | Validate inventory availability before accepting an order | The order is accepted (Confirmed) only after stock is reserved and the payment is authorized; otherwise it is Rejected and anything held is released |
| FR6 | Calculate pricing, including taxes and additional charges | Pipeline step CalculatePrice applies pricing rules (VAT per line, shipping charge) and stores a price snapshot; the price is pushed to the client through SignalR |
| FR7 | Integrate with external inventory, payment and shipping systems | Integration modules (anti-corruption layer) behind gateway interfaces; in M1 they are backed by in-memory fakes with configurable failure modes |

## 3. Non-functional requirements

| Requirement | How the architecture addresses it |
|---|---|
| Modularity | Modular monolith; each module owns its schema and exposes only a `.Contracts` project; architecture tests block forbidden references |
| Maintainability | Generic saga engine separated from order logic; pipeline defined in code; ADRs; team guide |
| Testability | Gateways behind interfaces; fakes with failure modes; `TimeProvider`; Testcontainers for database behaviour |
| Scalability | Stateless API and worker scale independently; lease-based run processing; modules can be extracted along their contracts |
| Reliability | Orchestrated saga with compensation; state saved after every step; idempotency keys; outbox; retries with backoff; RequiresAttention instead of silent failure |
| Security | Token-based auth with roles (Customer, BackOffice, Carrier); owner-only order access; payment tokens only, never card data; signed webhooks |
| Observability | OpenTelemetry traces and metrics, correlation id, structured logs; alerts on stuck orders |
| Deployment | Docker images and docker-compose locally; target Azure Container Apps, Azure SQL, Key Vault, Application Insights; migration bundle in CI/CD |
| Extensibility | New step means a new pipeline version; new provider means a new gateway implementation; new charge or promotion means a new pricing rule; optional steps through `ShouldRun` |

## 4. Core design

### Order fulfilment pipeline (saga)

| # | Step | Type | Compensation |
|---|---|---|---|
| 1 | CalculatePrice | Compensatable | None needed (snapshot kept for audit) |
| 2 | ReserveStock | Compensatable | Release reservation |
| 3 | AuthorizePayment | Compensatable | Void authorization |
| 4 | PrepareShipment | Compensatable | Cancel shipment booking |
| 5 | CapturePayment | **Pivot** | None. If it fails, compensate 4 to 1. Once it succeeds, only forward |
| 6 | DispatchShipment | Forward only | None. Retry, then RequiresAttention |
| 7 | AwaitDelivery | Forward only, OnDemand | None. Triggered by carrier webhook or back-office; timeout leads to RequiresAttention |

Rules:
- On a fatal error, undo every succeeded step in reverse order. Skipped steps are never undone.
- Step results: Success, RetryableError, FatalError, Waiting, Interrupted (shutdown, not a failure).
- A run is pinned to the pipeline version it started with.

### Statuses
- **Order:** Pending, Priced, Confirmed (accepted), Paid, Shipped, Delivered, Cancelled, Rejected, Failed.
- **Run:** Running, Waiting, Compensating, Completed, Compensated, RequiresAttention.

### Live updates
The worker writes an `OrderStatusChanged` message to the outbox in the same transaction as each step. An outbox dispatcher hosted in the API pushes it to the SignalR group `order-{id}`. Messages carry a sequence number; `GET /orders/{id}` remains the source of truth.

## 5. Scope of M1 (this submission)

**In scope (built and tested):**
- Solution structure and module boundaries, with architecture tests.
- Persistence: one schema per module, audit interceptor, seeded system user, enums as strings, seed data (users, products, prices, VAT categories, shipping rates).
- Process engine: persisted runner, simple polling worker, retries with backoff, compensation, ShouldRun, OnDemand steps.
- The 7 steps and pipeline V1, with pricing rules for VAT and shipping.
- Fake inventory, payment and shipping systems with failure modes (for example, card declined for SKU `TEST-DECLINE`).
- Endpoints: products, create, get, list, cancel, delivery trigger.
- SignalR hub with outbox dispatcher.
- docker-compose, `demo.http`, CI workflow file.

**Out of scope (designed, delivered as milestones):**
- Idempotency-Key on create, multi-worker hardening, pipeline versioning beyond V1 (M2).
- Real inventory, payment and shipping integrations (M3 to M5).
- Back-office operations for stuck orders (M6).
- Identity provider, production deployment, dashboards and alerts, SignalR scale-out (M7).
- Promotions, UI, returns, partial shipments, message bus (later).

## 6. Assumptions

- About 10,000 orders per day, peaks of 10x.
- One country, one currency (EUR), VAT per product category.
- External systems are reachable over HTTPS, may be slow or unavailable for minutes, and accept idempotency keys.
- One delivery per order. Card authorizations stay valid for 7 days.

## 7. Deliverables

- `README.md`: goal, how to run, demo script, what is left out and why, how AI was used, time spent.
- `docs/architecture.md`, `docs/adr/`, `docs/diagrams/`, `docs/milestones.md`, `docs/team-guide.md`, API and event contracts.
- The source code and tests described in section 5.

## 8. Glossary

- **Pipeline / saga:** the ordered list of steps that fulfils an order.
- **Run:** one execution of a pipeline for one order.
- **Compensation:** the undo action of a step.
- **Pivot:** the step after which no automatic undo is possible (CapturePayment).
- **Gateway:** the interface through which the platform talks to an external system.
- **RequiresAttention:** a run that needs a human decision.
