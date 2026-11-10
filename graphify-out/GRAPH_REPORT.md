# Graph Report - src  (2026-11-10)

## Corpus Check
- Corpus is ~24,165 words - fits in a single context window. You may not need a graph.

## Summary
- 1062 nodes · 2641 edges · 50 communities (44 shown, 6 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 209 edges (avg confidence: 0.81)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Saga Executor
- Catalog Reader
- Pipeline Run State
- Pricing Rules
- Orders API
- Saga Executor 2
- Fulfilment Steps
- Fulfilment Steps 2
- Fulfilment Steps 3
- Saga Executor 3
- Catalog Products
- Fulfilment Steps 4
- Background Worker
- Audit Fields
- Sign-in
- Fulfilment Steps 5
- Fulfilment Steps 6
- Fulfilment Steps 7
- Catalog Products 2
- Order Status Rules
- Order Aggregate
- Saga Executor 4
- Step Executions
- Pricing Rules 2
- Fulfilment Steps 8
- Order Aggregate 2
- Saga Executor 5
- Background Worker 2
- Orders API 2
- Saga Executor 6
- Catalog Products 3
- Step Executions 2
- Background Worker 3
- Pricing Rules 3
- Pricing Rules 4
- Rate Limiting
- Catalog Products 4
- Money
- Order Aggregate 3
- Community 40
- Fulfilment Steps 9
- Money 2
- Catalog Products 5
- Order Status Rules 2
- Order Aggregate 4
- Pipeline Run State 2
- Order Aggregate 5
- Pricing Rules 5
- Community 49

## God Nodes (most connected - your core abstractions)
1. `PipelineRun` - 81 edges
2. `OrderFulfilmentContext` - 53 edges
3. `Order` - 49 edges
4. `GatewayResult` - 38 edges
5. `StepExecution` - 33 edges
6. `Money` - 27 edges
7. `StepResult` - 27 edges
8. `PipelineExecutor` - 22 edges
9. `Product` - 22 edges
10. `ProductPrice` - 21 edges

## Surprising Connections (you probably didn't know these)
- `Product` --inherits--> `AuditableEntity`  [EXTRACTED]
  Modules/Catalog/OrderPlatform.Catalog/Products/Product.cs → Core/OrderPlatform.BuildingBlocks/Auditing/AuditableEntity.cs
- `ProductPrice` --inherits--> `AuditableEntity`  [EXTRACTED]
  Modules/Catalog/OrderPlatform.Catalog/Products/ProductPrice.cs → Core/OrderPlatform.BuildingBlocks/Auditing/AuditableEntity.cs
- `TaxCategory` --inherits--> `AuditableEntity`  [EXTRACTED]
  Modules/Catalog/OrderPlatform.Catalog/Products/TaxCategory.cs → Core/OrderPlatform.BuildingBlocks/Auditing/AuditableEntity.cs
- `Order` --inherits--> `AuditableEntity`  [EXTRACTED]
  Modules/Ordering/OrderPlatform.Ordering/Orders/Order.cs → Core/OrderPlatform.BuildingBlocks/Auditing/AuditableEntity.cs
- `OrderItem` --inherits--> `AuditableEntity`  [EXTRACTED]
  Modules/Ordering/OrderPlatform.Ordering/Orders/OrderItem.cs → Core/OrderPlatform.BuildingBlocks/Auditing/AuditableEntity.cs

## Import Cycles
- None detected.

## Communities (50 total, 6 thin omitted)

### Community 0 - "Saga Executor"
Cohesion: 0.07
Nodes (21): Result, Error, IsSuccess, Value, InlineRunOutcome, Failed, InProgress, Reached (+13 more)

### Community 1 - "Catalog Reader"
Cohesion: 0.05
Nodes (26): Page, HasNextPage, TotalPages, PageRequest, PageNumber, PageSize, Skip, OrderPlatform.Catalog.Contracts (+18 more)

### Community 2 - "Pipeline Run State"
Cohesion: 0.06
Nodes (10): AppDbContext, AppDbContextDesignTimeFactory, AuditInterceptor, InitialCreate, AddPipelineRuns, AppDbContextModelSnapshot, PersistenceServiceCollectionExtensions, OrderPlatform.Persistence (+2 more)

### Community 3 - "Pricing Rules"
Cohesion: 0.06
Nodes (36): Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.EntityFrameworkCore.Relational, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.NET.Sdk, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.NET.Sdk, Microsoft.NET.Sdk (+28 more)

### Community 4 - "Orders API"
Cohesion: 0.08
Nodes (11): OffersController, OrdersController, AddressModel, CreateOrderRequest, OrderItemResponse, OrderLineRequest, OrderPriceResponse, OrderResponse (+3 more)

### Community 5 - "Saga Executor 2"
Cohesion: 0.06
Nodes (25): PipelineExecutor, PipelineName, PipelineRun, BusinessKey, CanCancel, CancelRequestedAt, CompensationCause, ContextJson (+17 more)

### Community 6 - "Fulfilment Steps"
Cohesion: 0.09
Nodes (20): IPipelineStep, OrderFulfilmentContext, AuthorizationId, CaptureId, DeliveredAt, Lines, OrderId, PaymentToken (+12 more)

### Community 7 - "Fulfilment Steps 2"
Cohesion: 0.12
Nodes (10): GatewayOutcome, PermanentFailure, Succeeded, TransientFailure, GatewayResult, GatewayResult, IsSuccess, ConsumeStockRequest (+2 more)

### Community 8 - "Fulfilment Steps 3"
Cohesion: 0.10
Nodes (16): OrderPlatform.FakeExternalSystems.Inventory, OrderPlatform.Integrations.Inventory.Contracts, OrderPlatform.Integrations.Payments, OrderPlatform.Integrations.Inventory, OrderPlatform.FakeExternalSystems.Payments, OrderPlatform.FakeExternalSystems.Shipping, OrderPlatform.BuildingBlocks.Integration, OrderPlatform.Ordering.Fulfilment.Steps (+8 more)

### Community 9 - "Saga Executor 3"
Cohesion: 0.09
Nodes (19): StepExecution, AttemptCount, Attempts, CompletedAt, Kind, LastError, RunId, Status (+11 more)

### Community 10 - "Catalog Products"
Cohesion: 0.09
Nodes (13): TaxCategoryConfiguration, CatalogSeedData, ProductRows, TaxCategoryRows, SeedProduct, Id, PreviousPriceId, PriceId (+5 more)

### Community 11 - "Fulfilment Steps 4"
Cohesion: 0.13
Nodes (9): FakeExternalSystemsOptions, Inventory, FakeInventoryOptions, OutOfStockSkus, UnavailableSkus, FakeInventoryGateway, ReserveStockRequest, StockLine (+1 more)

### Community 12 - "Background Worker"
Cohesion: 0.16
Nodes (3): PipelineLog, GlobalExceptionHandler, RateLimitingSetup

### Community 13 - "Audit Fields"
Cohesion: 0.11
Nodes (15): AuditableEntity, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy, IAuditableEntity, CreatedAt, CreatedBy (+7 more)

### Community 14 - "Sign-in"
Cohesion: 0.12
Nodes (5): OrderPlatform.Api.Authentication, OrderPlatform.Api.Hosting, AuthenticationSetup, DemoUserAuthenticationHandler, ApiEnvironments

### Community 15 - "Fulfilment Steps 5"
Cohesion: 0.18
Nodes (5): AuthorizePaymentRequest, CapturePaymentRequest, PaymentAuthorization, PaymentCapture, GatewayStepResults

### Community 16 - "Fulfilment Steps 6"
Cohesion: 0.15
Nodes (6): Error, OrderPlatform.BuildingBlocks.Results, OrderPlatform.Pricing.Contracts, ErrorResponses, OrderErrors, PricingErrors

### Community 17 - "Fulfilment Steps 7"
Cohesion: 0.18
Nodes (8): OrderPlatform.Integrations.Shipping.Contracts, OrderPlatform.Integrations.Shipping, FakeShippingGateway, IShippingGateway, CancelShipmentRequest, ShippingGateway, ShippingIntegrationServiceCollectionExtensions, Step04PrepareShipment

### Community 18 - "Catalog Products 2"
Cohesion: 0.15
Nodes (6): OrderPlatform.Catalog.Products, OrderPlatform.BuildingBlocks.Auditing, OrderPlatform.Ordering.Persistence, OrderPlatform.Ordering.Orders, CatalogSchema, OrderingSchema

### Community 19 - "Order Status Rules"
Cohesion: 0.14
Nodes (12): OrderPlatform.Ordering.Fulfilment, OrderProgress, OrderStatus, Cancelled, Confirmed, Delivered, Failed, Paid (+4 more)

### Community 20 - "Order Aggregate"
Cohesion: 0.13
Nodes (10): ICurrentUser, UserId, SystemCurrentUser, UserId, SystemUser, OrderPlatform.Api.Users, OrderPlatform.Catalog.Persistence, OrderPlatform.BuildingBlocks.Users (+2 more)

### Community 22 - "Step Executions"
Cohesion: 0.13
Nodes (13): StepAttempt, DurationMilliseconds, Error, Number, Outcome, StartedAt, StepExecutionId, StepOutcome (+5 more)

### Community 23 - "Pricing Rules 2"
Cohesion: 0.19
Nodes (9): OrderPlatform.Pricing.Calculation, OrderPlatform.Pricing.Rules, PricingOptions, FreeShippingFrom, ShippingCharge, PricingService, IPricingRule, ShippingChargeRule (+1 more)

### Community 24 - "Fulfilment Steps 8"
Cohesion: 0.22
Nodes (5): BookShipmentRequest, ShipmentBooking, ShipmentLine, ShippingAddress, FulfilmentLine

### Community 25 - "Order Aggregate 2"
Cohesion: 0.12
Nodes (15): Order, CanCancel, Currency, CustomerId, Items, PaymentToken, RowVersion, ShippingAddress (+7 more)

### Community 26 - "Saga Executor 5"
Cohesion: 0.21
Nodes (3): OrderPlatform.ProcessEngine.Steps, OrderPlatform.ProcessEngine.Execution, OrderPlatform.ProcessEngine.Runs

### Community 27 - "Background Worker 2"
Cohesion: 0.17
Nodes (6): OrderPlatform.ProcessEngine, OrderPlatform.ProcessEngine.Pipelines, OrderPlatform.ProcessEngine.Storage, PaymentsIntegrationServiceCollectionExtensions, OrderingServiceCollectionExtensions, PricingServiceCollectionExtensions

### Community 28 - "Orders API 2"
Cohesion: 0.17
Nodes (5): OrderPlatform.Api.Models, OrderPlatform.Api.Errors, OrderPlatform.Ordering.Placing, OrderPlatform.Api.Controllers, ProblemDetailsSetup

### Community 29 - "Saga Executor 6"
Cohesion: 0.14
Nodes (8): PipelineStepDefinition, StepKind, Compensatable, ForwardOnly, Pivot, TriggerMode, Auto, OnDemand

### Community 30 - "Catalog Products 3"
Cohesion: 0.15
Nodes (7): ProductPrice, Amount, Currency, Price, ProductId, ValidFrom, ValidTo

### Community 31 - "Step Executions 2"
Cohesion: 0.22
Nodes (6): PipelineRunConfiguration, ProcessSchema, StepAttemptConfiguration, StepExecutionConfiguration, OrderPlatform.ProcessEngine.Persistence, ProductPriceConfiguration

### Community 32 - "Background Worker 3"
Cohesion: 0.24
Nodes (6): PipelineDefinition, Name, Steps, Version, PipelineDefinitionValidator, ProcessEngineServiceCollectionExtensions

### Community 33 - "Pricing Rules 3"
Cohesion: 0.20
Nodes (8): PriceCalculation, Currency, HasPhysicalItems, Lines, ShippingCharge, Subtotal, Tax, Total

### Community 34 - "Pricing Rules 4"
Cohesion: 0.20
Nodes (4): OrderPlatform.FakeExternalSystems, OrderPlatform.Ordering, OrderPlatform.Pricing, Program

### Community 36 - "Rate Limiting"
Cohesion: 0.17
Nodes (5): OrderPlatform.Api.RateLimiting, OrderPlatform.Api.Orders, RateLimitingOptions, PermitLimit, WindowSeconds

### Community 37 - "Catalog Products 4"
Cohesion: 0.20
Nodes (7): OrderItem, IsPhysical, OrderId, ProductId, Quantity, Sku, OrderItemConfiguration

### Community 39 - "Order Aggregate 3"
Cohesion: 0.18
Nodes (6): RetrySchedule, RetryPolicy, OrderFulfilmentPipelineV1, Name, Steps, Version

### Community 40 - "Community 40"
Cohesion: 0.18
Nodes (10): RunDirection, Backward, Forward, RunStatus, Compensated, Compensating, Completed, RequiresAttention (+2 more)

### Community 41 - "Fulfilment Steps 9"
Cohesion: 0.33
Nodes (3): PriceSnapshot, PriceRequest, PriceRequestLine

### Community 42 - "Money 2"
Cohesion: 0.20
Nodes (6): Currency, EUR, GBP, USD, OrderPlatform.BuildingBlocks.Money, OfferResponse

### Community 43 - "Catalog Products 5"
Cohesion: 0.20
Nodes (9): PricedLine, IsPhysical, NetAmount, ProductId, Quantity, Sku, TaxAmount, TaxRate (+1 more)

### Community 44 - "Order Status Rules 2"
Cohesion: 0.33
Nodes (4): IPipelineObserver, OrderProgressObserver, IOrderRepository, OrderRepository

### Community 45 - "Order Aggregate 4"
Cohesion: 0.25
Nodes (7): OrderStatusHistory, FromStatus, OrderId, Reason, Sequence, ToStatus, OrderStatusHistoryConfiguration

### Community 47 - "Order Aggregate 5"
Cohesion: 0.33
Nodes (4): OrderRequestOptions, MaxWait, MaxWaitSeconds, OrderRequestSetup

## Knowledge Gaps
- **220 isolated node(s):** `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`, `CreatedAt` (+215 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 338 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **6 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `OrderFulfilmentContext` connect `Fulfilment Steps` to `Saga Executor`, `Catalog Reader`, `Saga Executor 7`, `Order Aggregate 3`, `Fulfilment Steps 2`, `Fulfilment Steps 9`, `Fulfilment Steps 4`, `Order Status Rules 2`, `Fulfilment Steps 5`, `Fulfilment Steps 7`, `Saga Executor 4`, `Fulfilment Steps 8`?**
  _High betweenness centrality (0.093) - this node is a cross-community bridge._
- **What connects `CreatedAt`, `CreatedBy`, `ModifiedAt` to the rest of the system?**
  _220 weakly-connected nodes found - possible missing edges._
- **Should `Saga Executor` be split into smaller, more focused modules?**
  _Cohesion score 0.07113543091655267 - nodes in this community are weakly interconnected._
- **Why does `PipelineRun` connect `Saga Executor 2` to `Saga Executor`, `Catalog Reader`, `Saga Executor 7`, `Community 40`, `Saga Executor 3`, `Audit Fields`, `Pipeline Run State 2`, `Saga Executor 4`, `Saga Executor 5`, `Step Executions 2`?**
  _High betweenness centrality (0.085) - this node is a cross-community bridge._
- **Should `Catalog Reader` be split into smaller, more focused modules?**
  _Cohesion score 0.05325140809011777 - nodes in this community are weakly interconnected._
- **Why does `StepExecution` connect `Saga Executor 3` to `Saga Executor`, `Catalog Reader`, `Saga Executor 7`, `Saga Executor 2`, `Audit Fields`, `Saga Executor 4`, `Step Executions`, `Saga Executor 5`, `Step Executions 2`?**
  _High betweenness centrality (0.069) - this node is a cross-community bridge._
- **Should `Pipeline Run State` be split into smaller, more focused modules?**
  _Cohesion score 0.06229508196721312 - nodes in this community are weakly interconnected._