using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OrderPlatform.BuildingBlocks.Paging;
using OrderPlatform.BuildingBlocks.Results;
using OrderPlatform.Ordering.Fulfilment;
using OrderPlatform.Ordering.Orders;
using OrderPlatform.Ordering.Placing;
using OrderPlatform.Ordering.Tests.Support;
using OrderPlatform.Pricing.Contracts;
using OrderPlatform.ProcessEngine.Execution;
using OrderPlatform.ProcessEngine.Runs;
using OrderPlatform.ProcessEngine.Storage;
using static OrderPlatform.Ordering.Tests.Support.OrderingTestData;

namespace OrderPlatform.Ordering.Tests.Placing;

/// <summary>The order use cases end to end inside the module: real engine, real repository, SQLite.</summary>
public sealed class OrderServiceTests : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private readonly OrderingHarness _harness = new();

    [Fact]
    public async Task Place_WithoutWaiting_SavesThePendingOrderAndItsRun_WithoutRunningAnyStep()
    {
        var placed = await PlaceAsync(Command(wait: null));

        Assert.False(placed.PricingFinished);
        var (order, run) = await LoadAsync(placed.Order.Id);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(RunStatus.Running, run.Status);
        Assert.Equal(0, run.NextStepIndex);
        Assert.Equal(1, run.PipelineVersion);
        await _harness.Pricing.DidNotReceive().CalculateAsync(Arg.Any<PriceRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Place_WaitingForThePrice_ReturnsAndStoresThePricedOrder()
    {
        var placed = await PlaceAsync(Command(wait: Wait));

        Assert.True(placed.PricingFinished);
        Assert.Equal(OrderStatus.Priced, placed.Order.Status);
        var (order, run) = await LoadAsync(placed.Order.Id);
        Assert.Equal(OrderStatus.Priced, order.Status);
        Assert.Equal(104.08m, order.TotalAmount);
        Assert.Equal([OrderStatus.Pending, OrderStatus.Priced], order.StatusHistory.OrderBy(change => change.Sequence).Select(change => change.ToStatus));
        Assert.Equal(2, run.NextStepIndex);
        Assert.Null(run.LockedBy);
        Assert.Contains("104.08", run.ContextJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Place_Waiting_ReservesTheStockBeforeItCalculatesThePrice()
    {
        var placed = await PlaceAsync(Command(wait: Wait));

        var (_, run) = await LoadAsync(placed.Order.Id);
        var context = PipelineContextSerializer.Deserialize<OrderFulfilmentContext>(run.ContextJson);
        Assert.Equal(["ReserveStock", "CalculatePrice"], run.Steps.OrderBy(step => step.StepIndex).Select(step => step.StepKey));
        Assert.All(run.Steps, step => Assert.Equal(StepExecutionStatus.Succeeded, step.Status));
        Assert.True(_harness.IsReserved(context.ReservationId!));
    }

    [Fact]
    public async Task Place_WaitingButAnItemIsOutOfStock_RejectsTheOrderWithoutCalculatingAPrice()
    {
        _harness.Inventory.OutOfStockSkus.Add(Keyboard.Sku);

        var placed = await PlaceAsync(Command(wait: Wait));

        Assert.True(placed.PricingFinished);
        var (order, run) = await LoadAsync(placed.Order.Id);
        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Contains("Out of stock: KEYBOARD-MX", order.StatusHistory.OrderBy(change => change.Sequence).Last().Reason, StringComparison.Ordinal);
        Assert.Null(order.TotalAmount);
        Assert.Equal(RunStatus.Compensating, run.Status);
        await _harness.Pricing.DidNotReceive().CalculateAsync(Arg.Any<PriceRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Place_WaitingButTheInventoryIsUnavailable_LeavesTheOrderPendingForTheWorkerToRetry()
    {
        _harness.Inventory.UnavailableSkus.Add(Keyboard.Sku);

        var placed = await PlaceAsync(Command(wait: Wait));

        Assert.False(placed.PricingFinished);
        var (order, run) = await LoadAsync(placed.Order.Id);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(RunStatus.Running, run.Status);
        Assert.True(run.NextAttemptAt > _harness.Clock.GetUtcNow());
    }

    [Fact]
    public async Task Place_Waiting_DigitalOnlyOrder_SkipsTheStockReservation()
    {
        var placed = await PlaceAsync(Command(wait: Wait, new RequestedLine(Ebook.ProductId, 1)));

        var (order, run) = await LoadAsync(placed.Order.Id);
        Assert.Equal(OrderStatus.Priced, order.Status);
        Assert.Equal(StepExecutionStatus.Skipped, run.Steps.Single(step => step.StepKey == "ReserveStock").Status);
    }

    [Fact]
    public async Task Place_PriceFailsAfterTheStockWasReserved_WorkerReleasesTheReservation()
    {
        _harness.PriceFails(PricingErrors.ProductsNotAvailable([Keyboard.ProductId]));
        var placed = await PlaceAsync(Command(wait: Wait));
        var reservationId = PipelineContextSerializer.Deserialize<OrderFulfilmentContext>((await LoadAsync(placed.Order.Id)).Run.ContextJson).ReservationId!;
        Assert.True(_harness.IsReserved(reservationId));

        // What the worker will do: keep advancing the run until it is finished undoing.
        await _harness.InScopeAsync(async services =>
        {
            var run = await services.GetRequiredService<IPipelineRunStore>().FindByBusinessKeyAsync(OrderFulfilmentPipelineV1.PipelineName, placed.Order.Id, CancellationToken.None);
            var executor = services.GetRequiredService<IPipelineExecutor>();
            while (await executor.AdvanceAsync(run!, CancellationToken.None))
            {
            }

            await services.GetRequiredService<IPipelineRunStore>().SaveChangesAsync(CancellationToken.None);
            return run!.Status;
        });

        var (order, finished) = await LoadAsync(placed.Order.Id);
        Assert.Equal(RunStatus.Compensated, finished.Status);
        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.False(_harness.IsReserved(reservationId));
    }

    [Fact]
    public async Task Place_WaitingButThePriceCannotBeCalculated_ReturnsTheRejectedOrder()
    {
        _harness.PriceFails(PricingErrors.ProductsNotAvailable([Keyboard.ProductId]));

        var placed = await PlaceAsync(Command(wait: Wait));

        Assert.True(placed.PricingFinished);
        var (order, run) = await LoadAsync(placed.Order.Id);
        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Contains("pricing.products_not_available", order.StatusHistory.OrderBy(change => change.Sequence).Last().Reason, StringComparison.Ordinal);
        Assert.Equal(RunStatus.Compensating, run.Status);
    }

    [Fact]
    public async Task Place_CopiesSkuAndPhysicalFlagFromTheCatalog()
    {
        var placed = await PlaceAsync(Command(wait: null, new RequestedLine(Keyboard.ProductId, 2), new RequestedLine(Ebook.ProductId, 1)));

        var (order, _) = await LoadAsync(placed.Order.Id);
        Assert.Equal(
            [("EBOOK-DDD", 1, false), ("KEYBOARD-MX", 2, true)],
            order.Items.OrderBy(item => item.Sku).Select(item => (item.Sku, item.Quantity, item.IsPhysical)));
    }

    [Fact]
    public async Task Place_SameProductTwice_FailsAndSavesNothing()
    {
        var result = await TryPlaceAsync(Command(wait: null, new RequestedLine(Keyboard.ProductId, 1), new RequestedLine(Keyboard.ProductId, 2)));

        Assert.Equal(OrderErrors.DuplicateProduct, result.Error);
        Assert.Equal(0, (await ListAsync(Alice)).TotalCount);
    }

    [Fact]
    public async Task Place_UnknownProduct_FailsNamingTheProduct()
    {
        var unknown = Guid.CreateVersion7();

        var result = await TryPlaceAsync(Command(wait: null, new RequestedLine(Keyboard.ProductId, 1), new RequestedLine(unknown, 1)));

        Assert.Equal("ordering.products_not_available", result.Error!.Code);
        Assert.Contains(unknown.ToString(), result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0, (await ListAsync(Alice)).TotalCount);
    }

    [Fact]
    public async Task Place_ProductsInDifferentCurrencies_Fails()
    {
        var result = await TryPlaceAsync(Command(wait: null, new RequestedLine(Keyboard.ProductId, 1), new RequestedLine(DollarMug.ProductId, 1)));

        Assert.Equal(OrderErrors.MixedCurrencies, result.Error);
    }

    [Fact]
    public async Task Get_OwnOrder_ReturnsIt()
    {
        var placed = await PlaceAsync(Command(wait: null));

        var result = await _harness.InScopeAsync(services => Service(services).GetAsync(placed.Order.Id, Alice, CancellationToken.None));

        Assert.Equal(placed.Order.Id, result.Value!.Id);
    }

    [Fact]
    public async Task Get_AnotherCustomersOrder_IsNotFound()
    {
        var placed = await PlaceAsync(Command(wait: null));

        var result = await _harness.InScopeAsync(services => Service(services).GetAsync(placed.Order.Id, Bob, CancellationToken.None));

        Assert.Equal(OrderErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Get_UnknownOrder_IsNotFound()
    {
        var result = await _harness.InScopeAsync(services => Service(services).GetAsync(Guid.CreateVersion7(), Alice, CancellationToken.None));

        Assert.Equal(OrderErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task List_ReturnsOnlyTheCustomersOrders_OnePageAtATime()
    {
        for (var order = 0; order < 3; order++)
        {
            await PlaceAsync(Command(wait: null));
        }

        await PlaceAsync(Command(wait: null) with { CustomerId = Bob });

        var page = await ListAsync(Alice, new PageRequest(1, 2));

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, summary => Assert.Equal(1, summary.ItemCount));
    }

    [Fact]
    public async Task Cancel_PricedOrder_RunsTheSagaBackward_ReleasesTheStockAndCancelsTheOrder()
    {
        var placed = await PlaceAsync(Command(wait: Wait));
        var reservationId = PipelineContextSerializer.Deserialize<OrderFulfilmentContext>((await LoadAsync(placed.Order.Id)).Run.ContextJson).ReservationId!;

        var result = await CancelAsync(placed.Order.Id, Alice);

        Assert.Equal(OrderStatus.Cancelled, result.Value!.Status);
        var (order, run) = await LoadAsync(placed.Order.Id);
        Assert.Equal([OrderStatus.Pending, OrderStatus.Priced, OrderStatus.Cancelled], order.StatusHistory.OrderBy(change => change.Sequence).Select(change => change.ToStatus));
        Assert.Equal(RunStatus.Compensated, run.Status);
        Assert.Equal(
            ["CalculatePrice", "ReserveStock"],
            run.Steps.Where(step => step.Kind == StepExecutionKind.Compensate).OrderByDescending(step => step.StepIndex).Select(step => step.StepKey));
        Assert.False(_harness.IsReserved(reservationId));
        Assert.Null(run.LockedBy);
    }

    [Fact]
    public async Task Cancel_OrderThatHasNotStartedYet_CancelsItWithNothingToUndo()
    {
        var placed = await PlaceAsync(Command(wait: null));

        var result = await CancelAsync(placed.Order.Id, Alice);

        Assert.Equal(OrderStatus.Cancelled, result.Value!.Status);
        var (_, run) = await LoadAsync(placed.Order.Id);
        Assert.Equal(RunStatus.Compensated, run.Status);
        Assert.Empty(run.Steps);
    }

    [Fact]
    public async Task Cancel_Twice_SecondTimeCannotBeCancelled()
    {
        var placed = await PlaceAsync(Command(wait: Wait));
        await CancelAsync(placed.Order.Id, Alice);

        var result = await CancelAsync(placed.Order.Id, Alice);

        Assert.Equal(OrderErrors.CannotCancel, result.Error);
    }

    [Fact]
    public async Task Cancel_AnotherCustomersOrder_IsNotFound()
    {
        var placed = await PlaceAsync(Command(wait: null));

        var result = await CancelAsync(placed.Order.Id, Bob);

        Assert.Equal(OrderErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Cancel_RejectedOrder_CannotBeCancelled()
    {
        _harness.PriceFails(PricingErrors.ProductsNotAvailable([Keyboard.ProductId]));
        var placed = await PlaceAsync(Command(wait: Wait));

        var result = await CancelAsync(placed.Order.Id, Alice);

        Assert.Equal(OrderErrors.CannotCancel, result.Error);
    }

    public void Dispose() => _harness.Dispose();

    private static IOrderService Service(IServiceProvider services) => services.GetRequiredService<IOrderService>();

    private static PlaceOrderCommand Command(TimeSpan? wait, params RequestedLine[] lines) =>
        new(Alice, lines.Length > 0 ? lines : [new RequestedLine(Keyboard.ProductId, 1)], Address, "tok_visa", wait);

    private Task<Result<PlacedOrder>> TryPlaceAsync(PlaceOrderCommand command) =>
        _harness.InScopeAsync(services => Service(services).PlaceAsync(command, CancellationToken.None));

    private async Task<PlacedOrder> PlaceAsync(PlaceOrderCommand command)
    {
        var result = await TryPlaceAsync(command);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private Task<Result<Order>> CancelAsync(Guid orderId, Guid customerId) =>
        _harness.InScopeAsync(services => Service(services).CancelAsync(orderId, customerId, CancellationToken.None));

    private Task<Page<OrderSummary>> ListAsync(Guid customerId, PageRequest? page = null) =>
        _harness.InScopeAsync(services => Service(services).ListAsync(customerId, page ?? new PageRequest(1, 20), CancellationToken.None));

    private Task<(Order Order, PipelineRun Run)> LoadAsync(Guid orderId) =>
        _harness.InScopeAsync(async services =>
        {
            var order = await services.GetRequiredService<IOrderRepository>().FindAsync(orderId, CancellationToken.None);
            var run = await services.GetRequiredService<IPipelineRunStore>().FindByBusinessKeyAsync(OrderFulfilmentPipelineV1.PipelineName, orderId, CancellationToken.None);
            return (order!, run!);
        });
}
