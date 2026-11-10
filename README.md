# Order Processing Platform

A solution skeleton for an order processing platform in .NET 10, built for the Solution Architect assessment.

## Read this first

This is a **prototype built in about six hours**. It shows how I would structure the system and where I would spend the effort. It is not finished and it very likely has bugs.

I moved fast and added things as they came to mind. In a real project the same work would go slower and more carefully: smaller steps, reviews, a real database in the test pipeline, and a discussion with the business before decisions like "until when may a customer cancel". What you see is the big picture of the problem and one defensible way to solve it, not production code.

## The challenge as I see it

Creating and reading orders is the easy part. The hard part is everything that happens after an order is placed:

- several external systems must be called in order (inventory, payment, shipping);
- each call can fail, time out or succeed without us hearing about it;
- when a later step fails, the earlier ones must be undone: stock released, money given back;
- the whole thing must survive a restart in the middle.

So the system needs a **flow**: a list of steps, what to do when a step succeeds, and what to do when it fails. That flow is the centre of this solution, and it is where most of my time went.

## The decision: a small saga engine of my own

Before writing anything I considered the existing tools, mainly a message broker (Azure Service Bus) and a background job library (Hangfire). I chose to write a small saga engine. The idea is simple:

- A process is an ordered list of **steps**.
- Every step has two actions: **do** and **undo**.
- When a step succeeds, the engine moves to the next one.
- When a step fails for good, the engine runs the **undo of every step already done, in reverse order**.
- A step that fails temporarily is retried with a growing delay.
- The state is saved after every single step, so a crash loses at most one step, and that step is safe to repeat.
- A process definition has a **version**. Adding or removing steps is a new version; orders already running finish on the version they started with.

For an order the steps are: reserve stock, calculate price, authorize payment, prepare shipment, capture payment, dispatch shipment, wait for delivery. A cancel is the same mechanism run backward: recall the parcel, refund the payment, cancel the shipment, release the stock, as far as the order got.

Why not Service Bus or Hangfire, what this choice costs, and when I would replace it with a product such as Temporal or MassTransit, is argued in `docs/process-engine.docx`. That document is the one I would read first.

## What is in the repository

| Where | What |
|---|---|
| `src/Core/OrderPlatform.ProcessEngine` | The saga engine. It knows nothing about orders |
| `src/Modules/Ordering` | The order, its status rules, the seven steps, the use cases |
| `src/Modules/Catalog`, `src/Modules/Pricing` | Products and prices; VAT and shipping charge rules |
| `src/Integrations` | One gateway interface per external system, with placeholder clients and in-memory fakes |
| `src/Core/OrderPlatform.Persistence` | One database, one schema per module, migrations |
| `src/Hosts/OrderPlatform.Api` | The HTTP API |
| `src/Hosts/OrderPlatform.Worker` | The background worker that moves orders forward |
| `tests` | 459 automated tests, including tests that enforce the module boundaries |

The style is a modular monolith: modules talk to each other only through small contracts projects, so a module can become its own service later without rewriting its neighbours.

## Documents

All in `docs`:

| Document | What it answers |
|---|---|
| `process-engine.docx` | Why a saga, why a polling worker and not a message bus or Hangfire, how the engine works, build or buy, known gaps |
| `technical-wiki.docx` | The architecture, the API (paging, rate limiting, errors), data, logging and trace id, configuration |
| `security.docx` | What protects the system today, what is stubbed, the threats, and the plan for each gap |
| `milestones.docx` | What this submission delivers and the plan from here to the first real order |
| `REQUIREMENTS.md` | Each requirement of the assessment and whether it is built or only designed |

## How to run it

You need the .NET 10 SDK and SQL Server LocalDB (it comes with Visual Studio).

```
dotnet run --project src/Hosts/OrderPlatform.Api --launch-profile OrderPlatform.Api
```

Or open `OrderPlatform.slnx` in Visual Studio, set `OrderPlatform.Api` as the startup project and press F5.

On the first start the API creates the database, seeds nine products and runs the background worker in the same process, so one command is enough.

Two ways to try it:

- **The demo script:** open `src/Hosts/OrderPlatform.Api/OrderPlatform.Api.http` in Visual Studio and send the requests from top to bottom. It walks through the catalog, the life of an order, security and errors, and a cancel that refunds and releases everything.
- **The API page:** `https://localhost:49378/scalar`. It opens already signed in as a demo customer, with an example order filled in.

To run the tests: `dotnet test`. They need no database.

## What is real and what is not

**Built and tested**
- The saga engine: steps, undo, retries, waiting for an outside event, cancel, versions, locking between the API and the worker.
- The order flow from creation to delivery, and cancel until delivery with refund.
- The API with paging, rate limiting, one error format with a trace id, and owner-only access.
- Pricing with VAT and a shipping charge.

**Stubbed on purpose**
- **The external systems.** Inventory, payment and shipping are interfaces with placeholder clients that always answer "success". No real stock, money or parcel moves. The interfaces, the idempotency keys and the error mapping are real; the HTTP clients are not written.
- **Authentication.** A demo header (`X-User-Id`) says who you are. Authorization on top of it is real: a customer sees only their own orders and only a carrier can confirm a delivery. The API refuses to start outside Development until real authentication is configured.

**Not built**
- Token authentication with an identity provider.
- Events to other systems and live updates to the client.
- Back-office screens for orders that get stuck.
- Containers, a CI pipeline, metrics and dashboards.
- Returns after delivery, promotions, partial shipments.

**Known weak spots**
- A customer may cancel after the parcel is dispatched. That is built (recall and refund), but whether the business wants it is an open question.
- The rate limit is counted per instance.
- More are listed at the end of `docs/technical-wiki.docx` and in `docs/security.docx`.

## How I used AI

I used Claude Code throughout, as the assessment expects. 
To keep the output consistent I set up a minimal agentic workflow:

| File | Role |
|---|---|
| `CLAUDE.md` | The working rules for every task: read the requirement first, reuse before writing, the architecture rules, the .NET conventions, the definition of done |
| `agents/test-writer.md` | A subagent that only writes tests. It may change files under `tests` and nothing else, so it cannot "fix" production code to make a test pass |
| `agents/change-reviewer.md` | A read-only subagent that builds, runs the tests and checks a change against the requirements and the architecture rules, then returns a verdict |
| `docs/REQUIREMENTS.md` | The single list of requirements the agents check against |

The idea is separation of duties: one agent writes, another writes the tests, a third reviews and cannot edit. With the six-hour limit this workflow was not applied to every change; it shows how I would run a longer project.

The architecture tests in `tests/OrderPlatform.Architecture.Tests` are the safety net behind all of this: whoever writes the code, human or AI, a change that breaks a module boundary fails the build.
