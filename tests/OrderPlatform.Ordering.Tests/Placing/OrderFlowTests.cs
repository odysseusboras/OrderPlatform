using Microsoft.Extensions.DependencyInjection;
using OrderPlatform.BuildingBlocks.Results;
using OrderPlatform.Ordering.Fulfilment;
using OrderPlatform.Ordering.Orders;
using OrderPlatform.Ordering.Placing;
using OrderPlatform.Ordering.Tests.Support;
using OrderPlatform.ProcessEngine.Execution;
using OrderPlatform.ProcessEngine.Runs;
using OrderPlatform.ProcessEngine.Storage;
using static OrderPlatform.Ordering.Tests.Support.OrderingTestData;

namespace OrderPlatform.Ordering.Tests.Placing;

/// <summary>The whole order lifecycle: real engine, worker, repository and fakes on SQLite.</summary>
public sealed class OrderFlowTests : IDisposable
{
    private readonly OrderingHarness _harness = new();

    [Fact]
    public async Task PlacedOrder_IsMovedByTheWorker_UntilItIsShippedAndWaitsForTheDelivery()
    {
        var orderId = await PlaceAsync(Keyboard.ProductId);

        var moved = await _harness.Worker().RunOnceAsync(CancellationToken.None);

        var (order, run) = await LoadAsync(orderId);
        Assert.Equal(1, moved);
        Assert.Equal(
            [OrderStatus.Pending, OrderStatus.Priced, OrderStatus.Confirmed, OrderStatus.Paid, OrderStatus.Shipped],
            History(order));
        Assert.Equal(104.08m, order.TotalAmount);
        Assert.Equal(RunStatus.Waiting, run.Status);
        Assert.False(run.PivotPassed);
        Assert.True(order.CanCancel);
        Assert.Null(run.LockedBy);
        Assert.Equal(0, await _harness.Worker().RunOnceAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmDelivery_ShippedOrder_CompletesTheRunAndDeliversTheOrder()
    {
        var orderId = await PlaceAsync(Keyboard.ProductId);
        await _harness.Worker().RunOnceAsync(CancellationToken.None);

        var confirmed = await ConfirmDeliveryAsync(orderId);
        await _harness.Worker().RunOnceAsync(CancellationToken.None);

        var (order, run) = await LoadAsync(orderId);
        Assert.True(confirmed.IsSuccess);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Contains("DeliveredAt", run.ContextJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmDelivery_Twice_SecondTimeIsRefused()
    {
        var orderId = await PlaceAsync(Keyboard.ProductId);
        await _harness.Worker().RunOnceAsync(CancellationToken.None);
        await ConfirmDeliveryAsync(orderId);

        var second = await ConfirmDeliveryAsync(orderId);

        Assert.Equal(OrderErrors.NotAwaitingDelivery, second.Error);
    }

    [Fact]
    public async Task ConfirmDelivery_OrderNotShippedYet_IsRefused()
    {
        var orderId = await PlaceAsync(Keyboard.ProductId);

        Assert.Equal(OrderErrors.NotAwaitingDelivery, (await ConfirmDeliveryAsync(orderId)).Error);
    }

    [Fact]
    public async Task ConfirmDelivery_UnknownOrder_IsNotFound() =>
        Assert.Equal(OrderErrors.NotFound, (await ConfirmDeliveryAsync(Guid.CreateVersion7())).Error);

    [Fact]
    public async Task DigitalOrder_CompletesWithoutStockOrShipping()
    {
        var orderId = await PlaceAsync(Ebook.ProductId);

        await _harness.Worker().RunOnceAsync(CancellationToken.None);

        var (order, run) = await LoadAsync(orderId);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(
            ["ReserveStock", "PrepareShipment", "DispatchShipment", "AwaitDelivery"],
            run.Steps.Where(step => step.Status == StepExecutionStatus.Skipped).OrderBy(step => step.StepIndex).Select(step => step.StepKey));
    }

    [Fact]
    public async Task OutOfStockOrder_IsRejectedByTheWorker_AndNothingStaysHeld()
    {
        _harness.Inventory.OutOfStockSkus.Add(Keyboard.Sku);
        var orderId = await PlaceAsync(Keyboard.ProductId);

        await _harness.Worker().RunOnceAsync(CancellationToken.None);

        var (order, run) = await LoadAsync(orderId);
        Assert.Equal([OrderStatus.Pending, OrderStatus.Rejected], History(order));
        Assert.Equal(RunStatus.Compensated, run.Status);
    }

    [Fact]
    public async Task InventoryUnavailable_IsRetriedByTheWorker_WhenTheRetryIsDue()
    {
        _harness.Inventory.UnavailableSkus.Add(Keyboard.Sku);
        var orderId = await PlaceAsync(Keyboard.ProductId);
        await _harness.Worker().RunOnceAsync(CancellationToken.None);
        Assert.Equal(OrderStatus.Pending, (await LoadAsync(orderId)).Order.Status);

        _harness.Inventory.UnavailableSkus.Clear();
        var tooEarly = await _harness.Worker().RunOnceAsync(CancellationToken.None);
        _harness.Clock.Advance(TimeSpan.FromSeconds(2));
        await _harness.Worker().RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, tooEarly);
        Assert.Equal(OrderStatus.Shipped, (await LoadAsync(orderId)).Order.Status);
    }

    [Fact]
    public async Task Cancel_ConfirmedOrder_UndoesShipmentPaymentAndStock_InReverseOrder()
    {
        var orderId = await PlaceAsync(Keyboard.ProductId);
        await AdvanceAsync(orderId, moves: 4);
        Assert.Equal(OrderStatus.Confirmed, (await LoadAsync(orderId)).Order.Status);

        var result = await _harness.InScopeAsync(services => Service(services).CancelAsync(orderId, Alice, CancellationToken.None));

        var (order, run) = await LoadAsync(orderId);
        Assert.Equal(OrderStatus.Cancelled, result.Value!.Status);
        Assert.Equal([OrderStatus.Pending, OrderStatus.Priced, OrderStatus.Confirmed, OrderStatus.Cancelled], History(order));
        Assert.Equal(RunStatus.Compensated, run.Status);
        Assert.Equal(
            ["PrepareShipment", "AuthorizePayment", "CalculatePrice", "ReserveStock"],
            run.Steps.Where(step => step.Kind == StepExecutionKind.Compensate).OrderByDescending(step => step.StepIndex).Select(step => step.StepKey));
        var context = PipelineContextSerializer.Deserialize<OrderFulfilmentContext>(run.ContextJson);
        Assert.Null(context.ShipmentId);
        Assert.Null(context.AuthorizationId);
        Assert.Null(context.ReservationId);
    }

    [Fact]
    public async Task Cancel_ShippedOrder_RecallsTheParcel_RefundsThePayment_AndReleasesTheStock()
    {
        var orderId = await PlaceAsync(Keyboard.ProductId);
        await _harness.Worker().RunOnceAsync(CancellationToken.None);
        var reservationId = PipelineContextSerializer.Deserialize<OrderFulfilmentContext>((await LoadAsync(orderId)).Run.ContextJson).ReservationId!;
        Assert.Equal(OrderStatus.Shipped, (await LoadAsync(orderId)).Order.Status);

        var result = await _harness.InScopeAsync(services => Service(services).CancelAsync(orderId, Alice, CancellationToken.None));

        var (order, run) = await LoadAsync(orderId);
        Assert.Equal(OrderStatus.Cancelled, result.Value!.Status);
        Assert.Equal(
            [OrderStatus.Pending, OrderStatus.Priced, OrderStatus.Confirmed, OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Cancelled],
            History(order));
        Assert.Equal(RunStatus.Compensated, run.Status);
        Assert.Equal(
            ["DispatchShipment", "CapturePayment", "PrepareShipment", "AuthorizePayment", "CalculatePrice", "ReserveStock"],
            run.Steps.Where(step => step.Kind == StepExecutionKind.Compensate).OrderByDescending(step => step.StepIndex).Select(step => step.StepKey));
        var context = PipelineContextSerializer.Deserialize<OrderFulfilmentContext>(run.ContextJson);
        Assert.Null(context.CaptureId);
        Assert.Null(context.AuthorizationId);
        Assert.Null(context.ShipmentId);
        Assert.Null(context.ReservationId);
        Assert.False(_harness.IsReserved(reservationId));
    }

    [Fact]
    public async Task Cancel_DeliveredOrder_IsRefused_BecauseItIsFinal()
    {
        var orderId = await PlaceAsync(Keyboard.ProductId);
        await _harness.Worker().RunOnceAsync(CancellationToken.None);
        await ConfirmDeliveryAsync(orderId);
        await _harness.Worker().RunOnceAsync(CancellationToken.None);

        var result = await _harness.InScopeAsync(services => Service(services).CancelAsync(orderId, Alice, CancellationToken.None));

        Assert.Equal(OrderErrors.CannotCancel, result.Error);
        Assert.Equal(OrderStatus.Delivered, (await LoadAsync(orderId)).Order.Status);
    }

    [Fact]
    public async Task Cancel_Twice_SecondTimeIsRefused()
    {
        var orderId = await PlaceAsync(Keyboard.ProductId);
        await _harness.Worker().RunOnceAsync(CancellationToken.None);
        await _harness.InScopeAsync(services => Service(services).CancelAsync(orderId, Alice, CancellationToken.None));

        var second = await _harness.InScopeAsync(services => Service(services).CancelAsync(orderId, Alice, CancellationToken.None));

        Assert.Equal(OrderErrors.CannotCancel, second.Error);
    }

    [Fact]
    public async Task HeldOrder_StopsBeforeThePayment_WithStockReservedAndThePriceKnown()
    {
        var orderId = await PlaceAsync(Keyboard.ProductId, holdBeforePayment: true);

        await _harness.Worker().RunOnceAsync(CancellationToken.None);

        var (order, run) = await LoadAsync(orderId);
        var context = PipelineContextSerializer.Deserialize<OrderFulfilmentContext>(run.ContextJson);
        Assert.Equal([OrderStatus.Pending, OrderStatus.Priced], History(order));
        Assert.True(order.CanCancel);
        Assert.Equal(RunStatus.Waiting, run.Status);
        Assert.NotNull(context.ReservationId);
        Assert.Null(context.AuthorizationId);
        Assert.Equal(0, await _harness.Worker().RunOnceAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Cancel_HeldOrder_ReleasesTheStock_AndNeverTouchesThePayment()
    {
        var orderId = await PlaceAsync(Keyboard.ProductId, holdBeforePayment: true);
        await _harness.Worker().RunOnceAsync(CancellationToken.None);
        var reservationId = PipelineContextSerializer.Deserialize<OrderFulfilmentContext>((await LoadAsync(orderId)).Run.ContextJson).ReservationId!;

        var result = await _harness.InScopeAsync(services => Service(services).CancelAsync(orderId, Alice, CancellationToken.None));

        var (order, run) = await LoadAsync(orderId);
        Assert.Equal(OrderStatus.Cancelled, result.Value!.Status);
        Assert.Equal([OrderStatus.Pending, OrderStatus.Priced, OrderStatus.Cancelled], History(order));
        Assert.Equal(RunStatus.Compensated, run.Status);
        Assert.False(_harness.IsReserved(reservationId));
        Assert.Equal(
            ["CalculatePrice", "ReserveStock"],
            run.Steps.Where(step => step.Kind == StepExecutionKind.Compensate).OrderByDescending(step => step.StepIndex).Select(step => step.StepKey));
    }

    [Fact]
    public async Task Release_HeldOrder_ContinuesWithThePayment_UntilItIsShipped()
    {
        var orderId = await PlaceAsync(Keyboard.ProductId, holdBeforePayment: true);
        await _harness.Worker().RunOnceAsync(CancellationToken.None);

        var released = await ReleaseAsync(orderId, Alice);
        await _harness.Worker().RunOnceAsync(CancellationToken.None);

        Assert.True(released.IsSuccess);
        Assert.Equal(OrderStatus.Shipped, (await LoadAsync(orderId)).Order.Status);
    }

    [Fact]
    public async Task Release_OrderThatIsNotOnHold_IsRefused()
    {
        var orderId = await PlaceAsync(Keyboard.ProductId);
        await _harness.Worker().RunOnceAsync(CancellationToken.None);

        Assert.Equal(OrderErrors.NotOnHold, (await ReleaseAsync(orderId, Alice)).Error);
        Assert.Equal(OrderStatus.Shipped, (await LoadAsync(orderId)).Order.Status);
    }

    [Fact]
    public async Task Release_SomeoneElsesOrder_IsNotFound()
    {
        var orderId = await PlaceAsync(Keyboard.ProductId, holdBeforePayment: true);
        await _harness.Worker().RunOnceAsync(CancellationToken.None);

        Assert.Equal(OrderErrors.NotFound, (await ReleaseAsync(orderId, Guid.CreateVersion7())).Error);
    }

    [Fact]
    public async Task ConfirmDelivery_HeldOrder_IsRefused_AndTheOrderStaysOnHold()
    {
        var orderId = await PlaceAsync(Keyboard.ProductId, holdBeforePayment: true);
        await _harness.Worker().RunOnceAsync(CancellationToken.None);

        Assert.Equal(OrderErrors.NotAwaitingDelivery, (await ConfirmDeliveryAsync(orderId)).Error);
        Assert.Equal(RunStatus.Waiting, (await LoadAsync(orderId)).Run.Status);
    }

    public void Dispose() => _harness.Dispose();

    private static IOrderService Service(IServiceProvider services) => services.GetRequiredService<IOrderService>();

    private static IEnumerable<OrderStatus> History(Order order) =>
        order.StatusHistory.OrderBy(change => change.Sequence).Select(change => change.ToStatus);

    private Task<Result<Order>> ReleaseAsync(Guid orderId, Guid customerId) =>
        _harness.InScopeAsync(services => Service(services).ReleaseAsync(orderId, customerId, CancellationToken.None));

    private async Task<Guid> PlaceAsync(Guid productId, bool holdBeforePayment = false)
    {
        var command = new PlaceOrderCommand(Alice, [new RequestedLine(productId, 1)], Address, "tok_visa", WaitForPrice: null, holdBeforePayment);
        var result = await _harness.InScopeAsync(services => Service(services).PlaceAsync(command, CancellationToken.None));
        return result.Value!.Order.Id;
    }

    private Task<Result<Order>> ConfirmDeliveryAsync(Guid orderId) =>
        _harness.InScopeAsync(services => Service(services).ConfirmDeliveryAsync(orderId, CancellationToken.None));

    /// <summary>Moves the run a fixed number of steps, to stop the flow at a chosen point.</summary>
    private Task<int> AdvanceAsync(Guid orderId, int moves) =>
        _harness.InScopeAsync(async services =>
        {
            var store = services.GetRequiredService<IPipelineRunStore>();
            var run = await store.FindByBusinessKeyAsync(OrderFulfilmentPipelineV1.PipelineName, orderId, CancellationToken.None);
            var executor = services.GetRequiredService<IPipelineExecutor>();
            for (var move = 0; move < moves; move++)
            {
                await executor.AdvanceAsync(run!, CancellationToken.None);
                await store.SaveChangesAsync(CancellationToken.None);
            }

            return moves;
        });

    private Task<(Order Order, PipelineRun Run)> LoadAsync(Guid orderId) =>
        _harness.InScopeAsync(async services =>
        {
            var order = await services.GetRequiredService<IOrderRepository>().FindAsync(orderId, CancellationToken.None);
            var run = await services.GetRequiredService<IPipelineRunStore>().FindByBusinessKeyAsync(OrderFulfilmentPipelineV1.PipelineName, orderId, CancellationToken.None);
            return (order!, run!);
        });
}
