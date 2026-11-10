using OrderPlatform.BuildingBlocks.Money;
using OrderPlatform.BuildingBlocks.Paging;
using OrderPlatform.BuildingBlocks.Results;
using OrderPlatform.Catalog.Contracts;
using OrderPlatform.Ordering.Fulfilment;
using OrderPlatform.Ordering.Fulfilment.Steps;
using OrderPlatform.Ordering.Orders;
using OrderPlatform.ProcessEngine.Execution;
using OrderPlatform.ProcessEngine.Storage;

namespace OrderPlatform.Ordering.Placing;

internal sealed class OrderService(
    IOrderRepository orders,
    ICatalogReader catalog,
    IPipelineRunStore runs,
    IPipelineRunner runner,
    OrderFulfilmentPipelineV1 pipeline,
    TimeProvider timeProvider) : IOrderService
{
    // ponytail: fixed budget; make it an option when someone needs to tune it.
    internal static readonly TimeSpan CancelBudget = TimeSpan.FromSeconds(10);

    public async Task<Result<PlacedOrder>> PlaceAsync(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var lines = await ToOrderLinesAsync(command.Lines, cancellationToken);
        if (!lines.IsSuccess)
        {
            return Result<PlacedOrder>.Failure(lines.Error!);
        }

        var (currency, orderLines) = lines.Value;
        var order = Order.Create(command.CustomerId, command.ShippingAddress, command.PaymentToken, currency, orderLines);
        orders.Add(order);
        var context = OrderFulfilmentContext.From(order);
        context.HoldBeforePayment = command.HoldBeforePayment;
        var run = runs.Start(pipeline, order.Id, context);

        if (command.WaitForPrice is not { } wait)
        {
            await orders.SaveChangesAsync(cancellationToken);
            return Result<PlacedOrder>.Success(new PlacedOrder(order, PricingFinished: false));
        }

        // The runner's first save stores the order and the locked run together, then runs the steps:
        // reserve the stock (the availability check), then calculate the price.
        var outcome = await runner.RunUntilAsync(run, Step02CalculatePrice.Key, wait, cancellationToken);
        return Result<PlacedOrder>.Success(new PlacedOrder(order, outcome != InlineRunOutcome.InProgress));
    }

    public async Task<Result<Order>> GetAsync(Guid orderId, Guid customerId, CancellationToken cancellationToken)
    {
        var order = await orders.FindAsync(orderId, cancellationToken);

        // Another customer's order is reported as missing, so order ids cannot be probed.
        return order is not null && order.CustomerId == customerId
            ? Result<Order>.Success(order)
            : Result<Order>.Failure(OrderErrors.NotFound);
    }

    public Task<Page<OrderSummary>> ListAsync(Guid customerId, PageRequest page, CancellationToken cancellationToken) =>
        orders.ListForCustomerAsync(customerId, page, cancellationToken);

    public async Task<Result<Order>> CancelAsync(Guid orderId, Guid customerId, CancellationToken cancellationToken)
    {
        var found = await GetAsync(orderId, customerId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found;
        }

        var order = found.Value!;
        var run = await runs.FindByBusinessKeyAsync(pipeline.Name, orderId, cancellationToken);
        if (run is null || !order.CanCancel || !run.RequestCancel(timeProvider.GetUtcNow()))
        {
            return Result<Order>.Failure(OrderErrors.CannotCancel);
        }

        // Run the saga backward now: every succeeded step is undone in reverse order (parcel recalled, payment refunded,
        // stock released, as far as the order got) and the order becomes Cancelled.
        // If an undo has to be retried, the order is already Cancelled and the worker finishes releasing what is held.
        await runner.RunCancellationAsync(run, CancelBudget, cancellationToken);
        return Result<Order>.Success(order);
    }

    public async Task<Result<Order>> ReleaseAsync(Guid orderId, Guid customerId, CancellationToken cancellationToken)
    {
        var found = await GetAsync(orderId, customerId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found;
        }

        // A run waits in two places: on hold before the payment (the order is Priced) and for the delivery (Shipped).
        var run = await runs.FindByBusinessKeyAsync(pipeline.Name, orderId, cancellationToken);
        if (run is null || found.Value!.Status != OrderStatus.Priced || !run.Trigger<OrderFulfilmentContext>(context => context.HoldBeforePayment = false, timeProvider.GetUtcNow()))
        {
            return Result<Order>.Failure(OrderErrors.NotOnHold);
        }

        await orders.SaveChangesAsync(cancellationToken);
        return found;
    }

    public async Task<Result<Order>> ConfirmDeliveryAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orders.FindAsync(orderId, cancellationToken);
        if (order is null)
        {
            return Result<Order>.Failure(OrderErrors.NotFound);
        }

        var now = timeProvider.GetUtcNow();
        var run = await runs.FindByBusinessKeyAsync(pipeline.Name, orderId, cancellationToken);
        if (run is null || order.Status != OrderStatus.Shipped || !run.Trigger<OrderFulfilmentContext>(context => context.DeliveredAt = now, now))
        {
            return Result<Order>.Failure(OrderErrors.NotAwaitingDelivery);
        }

        await orders.SaveChangesAsync(cancellationToken);
        return Result<Order>.Success(order);
    }

    /// <summary>Completes the requested lines from the catalog (SKU, physical or digital) and finds the order currency.</summary>
    private async Task<Result<(Currency Currency, List<NewOrderLine> Lines)>> ToOrderLinesAsync(
        IReadOnlyList<RequestedLine> requested, CancellationToken cancellationToken)
    {
        if (requested.DistinctBy(line => line.ProductId).Count() != requested.Count)
        {
            return Result<(Currency, List<NewOrderLine>)>.Failure(OrderErrors.DuplicateProduct);
        }

        var productIds = requested.Select(line => line.ProductId).ToList();
        var offers = (await catalog.GetOffersAsync(productIds, timeProvider.GetUtcNow(), cancellationToken))
            .ToDictionary(offer => offer.ProductId);

        var missing = productIds.Where(id => !offers.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            return Result<(Currency, List<NewOrderLine>)>.Failure(OrderErrors.ProductsNotAvailable(missing));
        }

        var currencies = offers.Values.Select(offer => offer.UnitPrice.Currency).Distinct().ToList();
        if (currencies.Count > 1)
        {
            return Result<(Currency, List<NewOrderLine>)>.Failure(OrderErrors.MixedCurrencies);
        }

        var lines = requested
            .Select(line => new NewOrderLine(line.ProductId, offers[line.ProductId].Sku, line.Quantity, offers[line.ProductId].IsPhysical))
            .ToList();
        return Result<(Currency, List<NewOrderLine>)>.Success((currencies[0], lines));
    }
}
